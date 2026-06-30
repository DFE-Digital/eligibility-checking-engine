# simulate-bulk-load.ps1
#
# Simulates the concurrent bulk check submission scenario that causes the SQL timeout bug.
# Submits multiple batches simultaneously, then polls their progress until all complete.
#
# PREREQUISITES: API running, Azurite running, run-queue-processor.ps1 running.
#
# USAGE:
#   .\simulate-bulk-load.ps1
#   .\simulate-bulk-load.ps1 -NumberOfBatches 10 -RecordsPerBatch 200
#   .\simulate-bulk-load.ps1 -CheckType early-year-pupil-premium -NumberOfBatches 3

param(
    # API base URL
    [string]$ApiBase = $(if ([string]::IsNullOrWhiteSpace($env:CYE_API_BASE_URL)) { "https://localhost:7117" } else { $env:CYE_API_BASE_URL }),

    # Check type endpoint — one of: free-school-meals | early-year-pupil-premium | two-year-offer
    [ValidateSet("free-school-meals", "early-year-pupil-premium", "two-year-offer")]
    [string]$CheckType = "free-school-meals",

    # How many bulk check batches to submit concurrently
    [int]$NumberOfBatches = 5,

    # Records per batch. Max is BulkEligibilityCheckLimit in appsettings (250 locally, 5000 dev)
    [int]$RecordsPerBatch = 100,

    # Milliseconds between each batch submission (0 = fire all simultaneously)
    [int]$SubmissionStaggerMs = 0,

    # API client credentials (must have bulk_check + local_authority scope)
    [string]$ClientId = $env:CYE_CLIENT_ID,
    [string]$ClientSecret = $env:CYE_CLIENT_SECRET,

    # NINO prefixes — controls the TestData outcome for lastName=TESTER.
    # Each record picks one at random from this list.
    # NE=eligibleExpanded | NN=eligible | PN=notEligible | RA=parentNotFound
    # NOTE: NT (eligibleTargeted) is excluded — it is blocked by HMRC NINO validation rules
    # and will cause a 400 for any batch containing it.
    [string[]]$NinoPrefixes = @("NE", "NN", "PN", "RA"),

    # How often to poll /bulk-check/{guid}/progress
    [int]$PollIntervalSeconds = 5,

    # Stop polling after this many minutes even if not all batches are done
    [int]$PollTimeoutMinutes = 10
)

# -----------------------------------------------------------------------
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiBase) -or
    [string]::IsNullOrWhiteSpace($ClientId) -or
    [string]::IsNullOrWhiteSpace($ClientSecret)) {
    throw "Provide ApiBase, ClientId and ClientSecret (or set CYE_API_BASE_URL, CYE_CLIENT_ID and CYE_CLIENT_SECRET)."
}

$apiEndpoint = "$ApiBase/bulk-check/$CheckType"
$totalRecords = $NumberOfBatches * $RecordsPerBatch

# Offset NINO indices by a run-unique number so each run generates NINOs
# that haven't been submitted before and won't hit the 7-day hash cache.
# Capped at 900000 so offset + max 500 records stays within the 6-digit
# NINO digit field (max 999999). Changes every second, so consecutive runs
# always get a different window.
$runIndexOffset = [int]([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() % 900000)

Write-Host ""
Write-Host "=== Bulk Check Load Simulator ===" -ForegroundColor Cyan
Write-Host "  API:              $ApiBase"
Write-Host "  Check type:       $CheckType"
Write-Host "  Batches:          $NumberOfBatches"
Write-Host "  Records/batch:    $RecordsPerBatch"
Write-Host "  Total records:    $totalRecords"
Write-Host "  Stagger:          ${SubmissionStaggerMs}ms between submissions"
  Write-Host "  NINO prefixes:    $($NinoPrefixes -join ', ') — randomised per record (lastName=TESTER)"
Write-Host ""

# -----------------------------------------------------------------------
# Step 1 — Authenticate
# -----------------------------------------------------------------------
Write-Host "Authenticating as '$ClientId'..." -ForegroundColor Yellow

$tokenBody = @{
    client_id     = $ClientId
    client_secret = $ClientSecret
    grant_type    = "client_credentials"
    scope         = "bulk_check local_authority check"
}

$tokenResponse = Invoke-RestMethod -Uri "$ApiBase/oauth2/token" `
    -Method Post -Body $tokenBody `
    -ContentType "application/x-www-form-urlencoded" `
    -SkipCertificateCheck

$token = $tokenResponse.access_token
if (-not $token) { throw "Failed to obtain access token." }
Write-Host "  Token obtained." -ForegroundColor Green

# -----------------------------------------------------------------------
# Step 2 — Build batch payloads
# -----------------------------------------------------------------------
Write-Host "Generating $NumberOfBatches batches of $RecordsPerBatch records..." -ForegroundColor Yellow

# Pre-build all batch payloads as JSON strings so the parallel block has no dependencies
$batchPayloads = @()
for ($b = 1; $b -le $NumberOfBatches; $b++) {
    $records = @()
    for ($r = 1; $r -le $RecordsPerBatch; $r++) {
        $recordIndex = $runIndexOffset + (($b - 1) * $RecordsPerBatch) + $r
        # Generate a unique NINO: random prefix + 6 zero-padded digits + C  (e.g. NT000001C)
        $prefix = $NinoPrefixes | Get-Random
        $nino = "$prefix{0:D6}C" -f $recordIndex
        # Vary DOBs so records look realistic
        $year  = 1970 + ($recordIndex % 25)
        $month = (($recordIndex % 12) + 1).ToString("D2")
        $day   = (($recordIndex % 28) + 1).ToString("D2")

        $records += @{
            lastName                    = "TESTER"
            dateOfBirth                 = "$year-$month-$day"
            nationalInsuranceNumber     = $nino
            clientIdentifier            = "batch${b}-record${r}"
        }
    }

    $payload = @{
        data = $records
        meta = @{
            filename    = "simulate-batch-$b.csv"
            submittedBy = "load-simulator"
        }
    }

    $batchPayloads += [PSCustomObject]@{
        BatchNumber = $b
        Json        = ($payload | ConvertTo-Json -Depth 5 -Compress)
    }
}
Write-Host "  Payloads ready." -ForegroundColor Green

# -----------------------------------------------------------------------
# Step 3 — Submit all batches concurrently
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "Submitting $NumberOfBatches batches concurrently..." -ForegroundColor Yellow
$submitStart = Get-Date

$submissionResults = $batchPayloads | ForEach-Object -Parallel {
    $batch       = $_
    $api         = $using:ApiBase
    $endpoint    = $using:apiEndpoint
    $bearerToken = $using:token
    $staggerMs   = $using:SubmissionStaggerMs

    # Optional stagger — delay each batch slightly to mimic real-world scheduler behaviour
    if ($staggerMs -gt 0) {
        Start-Sleep -Milliseconds ($batch.BatchNumber * $staggerMs)
    }

    $headers = @{ Authorization = "Bearer $bearerToken" }
    $result  = [PSCustomObject]@{
        BatchNumber = $batch.BatchNumber
        Guid        = $null
        StatusCode  = $null
        Error       = $null
        SubmittedAt = Get-Date
    }

    try {
        $response = Invoke-RestMethod -Uri $endpoint `
            -Method Post -Headers $headers `
            -Body $batch.Json -ContentType "application/json" `
            -SkipCertificateCheck

        # Response: { data: { status: "processing" }, links: { get_Progress_Check: "/bulk-check/{guid}/progress" } }
        $progressLink = $response.links.get_Progress_Check
        if ($progressLink -match '/bulk-check/([^/]+)/progress') {
            $result.Guid = $Matches[1]
        }
        $result.StatusCode = 202
    }
    catch {
        $result.StatusCode = $_.Exception.Response.StatusCode.value__
        $result.Error      = $_.Exception.Message
    }

    return $result

} -ThrottleLimit $NumberOfBatches

$submitEnd   = Get-Date
$submitMs    = [int]($submitEnd - $submitStart).TotalMilliseconds

Write-Host ""
Write-Host "Submission complete in ${submitMs}ms:" -ForegroundColor Green
$submissionResults | ForEach-Object {
    if ($_.Error) {
        Write-Host ("  Batch {0}: FAILED ({1}) — {2}" -f $_.BatchNumber, $_.StatusCode, $_.Error) -ForegroundColor Red
    } else {
        Write-Host ("  Batch {0}: submitted  guid={1}" -f $_.BatchNumber, $_.Guid) -ForegroundColor Gray
    }
}

# Filter to batches that were successfully submitted
$activeBatches = $submissionResults | Where-Object { -not $_.Error -and $_.Guid }
if ($activeBatches.Count -eq 0) {
    Write-Host "No batches were submitted successfully. Exiting." -ForegroundColor Red
    exit 1
}

# -----------------------------------------------------------------------
# Step 4 — Poll progress until all batches complete or timeout
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "Polling progress (every ${PollIntervalSeconds}s, timeout ${PollTimeoutMinutes}min)..." -ForegroundColor Yellow
Write-Host "  Keep run-queue-processor.ps1 running to process queue messages." -ForegroundColor DarkGray

$headers   = @{ Authorization = "Bearer $token" }
$deadline  = (Get-Date).AddMinutes($PollTimeoutMinutes)
$progress  = @{}   # guid -> last known progress object
$activeBatches | ForEach-Object { $progress[$_.Guid] = $null }

while ((Get-Date) -lt $deadline) {
    $allDone = $true

    foreach ($batch in $activeBatches) {
        $guid = $batch.Guid
        try {
            $p = Invoke-RestMethod -Uri "$ApiBase/bulk-check/$guid/progress" `
                -Method Get -Headers $headers -SkipCertificateCheck
            $progress[$guid] = $p.data
        }
        catch {
            # Progress endpoint may 404 briefly after submission — keep waiting
        }

        $p = $progress[$guid]
        if (-not $p -or $p.complete -lt $p.total) {
            $allDone = $false
        }
    }

    # Print status line
    $statusParts = $activeBatches | ForEach-Object {
        $p = $progress[$_.Guid]
        if ($p) { "Batch$($_.BatchNumber):$($p.complete)/$($p.total)" }
        else     { "Batch$($_.BatchNumber):pending" }
    }
    Write-Host "  $(Get-Date -Format 'HH:mm:ss')  $($statusParts -join '  |  ')" -ForegroundColor DarkGray

    if ($allDone) { break }
    Start-Sleep -Seconds $PollIntervalSeconds
}

# -----------------------------------------------------------------------
# Step 5 — Final summary
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "=== Final Summary ===" -ForegroundColor Cyan

$table = $activeBatches | ForEach-Object {
    $p        = $progress[$_.Guid]
    $total    = if ($p) { $p.total }    else { "?" }
    $complete = if ($p) { $p.complete } else { "?" }
    $status   = if ($p -and $p.complete -eq $p.total) { "DONE" }
                elseif ($p)                            { "PARTIAL" }
                else                                   { "NO DATA" }

    [PSCustomObject]@{
        Batch     = $_.BatchNumber
        Guid      = $_.Guid
        Total     = $total
        Complete  = $complete
        Status    = $status
    }
}

$table | Format-Table -AutoSize
$done    = ($table | Where-Object { $_.Status -eq "DONE" }).Count
$partial = ($table | Where-Object { $_.Status -eq "PARTIAL" }).Count
Write-Host "  $done/$($activeBatches.Count) batches fully processed.  $partial partial." -ForegroundColor $(if ($done -eq $activeBatches.Count) { "Green" } else { "Yellow" })
Write-Host ""
