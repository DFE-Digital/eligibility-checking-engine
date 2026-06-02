# Test-BulkCheckPerformance.ps1
#
# Calls GET /bulk-check/search?organisationId={id} and reports response time.
# Run against local or dev to verify the GetBulkStatuses performance fix.
#
# USAGE — local:
#   .\Test-BulkCheckPerformance.ps1
#
# USAGE — dev (supply credentials and a known large LA):
#   .\Test-BulkCheckPerformance.ps1 `
#       -ApiBase      "https://dev.eligibility-checking-engine.education.gov.uk" `
#       -ClientId     "<your-dev-client-id>" `
#       -ClientSecret "<your-dev-client-secret>" `
#       -OrganisationId "<la-id-with-large-volume>"

param(
    # API base URL
    # Local: https://localhost:7117
    # Dev:   https://dev.eligibility-checking-engine.education.gov.uk
    [string]$ApiBase        = "https://localhost:7117",

    # OAuth2 client credentials — must have bulk_check + local_authority scope
    [string]$ClientId       = "<your-client-id>",
    [string]$ClientSecret   = "<your-client-secret>",

    # Local authority ID to query.
    # On dev, use a known large LA (e.g. one referenced in the bug ticket).
    [string]$OrganisationId = "<your-organisation-id>",

    [int]$Runs = 3     # number of timed calls to average
)

$ErrorActionPreference = "Stop"

function Get-TimeColour([long]$ms) {
    if ($ms -lt 1000) { 'Green' } elseif ($ms -lt 5000) { 'Yellow' } else { 'Red' }
}

# ---------------------------------------------------------------------------
# Step 1 — Authenticate
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Authenticating as '$ClientId' against $ApiBase..." -ForegroundColor Yellow

$tokenBody = @{
    client_id     = $ClientId
    client_secret = $ClientSecret
    grant_type    = "client_credentials"
    scope         = "bulk_check local_authority check"
}

$tokenResponse = Invoke-RestMethod `
    -Uri "$ApiBase/oauth2/token" `
    -Method Post `
    -Body $tokenBody `
    -ContentType "application/x-www-form-urlencoded" `
    -SkipCertificateCheck

$token = $tokenResponse.access_token
if (-not $token) { throw "Failed to obtain access token." }
Write-Host "  Token obtained." -ForegroundColor Green

$headers = @{ Authorization = "Bearer $token" }
$url     = "$ApiBase/bulk-check/search?organisationId=$OrganisationId"

Write-Host ""
Write-Host "Target:  GET $url" -ForegroundColor Cyan
Write-Host "Runs:    $Runs"
Write-Host ""

# ---------------------------------------------------------------------------
# Step 2 — Warm-up (not timed — avoids JIT / connection setup skew)
# ---------------------------------------------------------------------------
Write-Host "Warm-up call..." -ForegroundColor DarkGray
try {
    $null = Invoke-RestMethod -Uri $url -Method Get -Headers $headers -SkipCertificateCheck
    Write-Host "  Warm-up OK." -ForegroundColor DarkGray
} catch {
    Write-Host "  Warm-up failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ---------------------------------------------------------------------------
# Step 3 — Timed runs
# ---------------------------------------------------------------------------
$times = @()

for ($run = 1; $run -le $Runs; $run++) {
    Write-Host "Run $run/$Runs..." -ForegroundColor Yellow -NoNewline

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $response = Invoke-RestMethod `
            -Uri $url -Method Get -Headers $headers -SkipCertificateCheck

        $sw.Stop()
        $ms    = $sw.ElapsedMilliseconds
        $times += $ms
        $count = ($response.checks | Measure-Object).Count

        Write-Host (" {0,6:N0} ms   ({1} BulkChecks returned)" -f $ms, $count) -ForegroundColor (Get-TimeColour $ms)
    } catch {
        $sw.Stop()
        Write-Host (" FAILED after {0:N0} ms — {1}" -f $sw.ElapsedMilliseconds, $_.Exception.Message) -ForegroundColor Red
    }
}

# ---------------------------------------------------------------------------
# Step 4 — Summary
# ---------------------------------------------------------------------------
if ($times.Count -gt 0) {
    $avg = ($times | Measure-Object -Average).Average
    $min = ($times | Measure-Object -Minimum).Minimum
    $max = ($times | Measure-Object -Maximum).Maximum

    Write-Host ""
    Write-Host "=== Results ===" -ForegroundColor Cyan
    Write-Host ("  Min:  {0,6:N0} ms" -f $min) -ForegroundColor (Get-TimeColour $min)
    Write-Host ("  Max:  {0,6:N0} ms" -f $max)
    Write-Host ("  Avg:  {0,6:N0} ms" -f $avg) -ForegroundColor (Get-TimeColour $avg)
    Write-Host ""
}
