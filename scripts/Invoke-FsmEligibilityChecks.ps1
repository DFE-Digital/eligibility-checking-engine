# Invoke-FsmEligibilityChecks.ps1
# Reusable script for ad-hoc "can we run a check for these people" requests.
#
# Reads a CSV of people (Name, LastName, DateOfBirth, NationalInsuranceNumber),
# submits each one to POST /check/free-school-meals, polls
# GET /check/{guid}/status until the async check has finished processing,
# and prints a summary table (also optionally exports it to CSV).
#
# CSV columns required:
#   Name                     - display name, e.g. "Georgina Calcutt" (not sent to the API)
#   LastName                 - surname as submitted for the check
#   DateOfBirth              - dd/MM/yyyy or yyyy-MM-dd
#   NationalInsuranceNumber  - e.g. JG299357B
#
# .EXAMPLE
#   $env:API_BASE_URL = "https://eligibility-checking-engine.education.gov.uk"
#   $env:CLIENT_ID = "..."
#   $env:CLIENT_SECRET = "..."
#   ./Invoke-FsmEligibilityChecks.ps1 -CsvPath ./sample-fsm-check-requests.csv
#
# .EXAMPLE
#   ./Invoke-FsmEligibilityChecks.ps1 -CsvPath ./my-batch.csv -OutputCsv ./my-batch-results.csv

param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [string]$OutputCsv,

    # How many times to poll a check's status before giving up (it starts as "queuedForProcessing").
    [int]$MaxPollAttempts = 15,

    [int]$PollDelaySeconds = 2
)

$ErrorActionPreference = "Stop"

$baseUrl = $env:API_BASE_URL
$clientId = $env:CLIENT_ID
$clientSecret = $env:CLIENT_SECRET
$scope = "local_authority check application admin bulk_check establishment user engine notification free_school_meals two_year_offer early_year_pupil_premium working_families multi_academy_trust"

if ([string]::IsNullOrWhiteSpace($baseUrl) -or
    [string]::IsNullOrWhiteSpace($clientId) -or
    [string]::IsNullOrWhiteSpace($clientSecret)) {
    Write-Error "Set API_BASE_URL, CLIENT_ID and CLIENT_SECRET environment variables before running this script."
    exit 1
}

if (-not (Test-Path $CsvPath)) {
    Write-Error "CSV file not found: $CsvPath"
    exit 1
}

# --- Token helper ---
function Get-Token {
    $tokenBody = @{
        client_id     = $clientId
        client_secret = $clientSecret
        grant_type    = "client_credentials"
        scope         = $scope
    }
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/oauth2/token" `
            -Method POST `
            -ContentType "application/x-www-form-urlencoded" `
            -Body $tokenBody
        if (-not $response.access_token) {
            throw "No access_token in response."
        }
        return $response.access_token
    }
    catch {
        Write-Error "Failed to get token: $_"
        exit 1
    }
}

# Accepts dd/MM/yyyy or yyyy-MM-dd, returns yyyy-MM-dd for the API.
function ConvertTo-ApiDate {
    param([string]$Value)
    $formats = @('dd/MM/yyyy', 'yyyy-MM-dd')
    foreach ($fmt in $formats) {
        $parsed = [datetime]::MinValue
        if ([datetime]::TryParseExact($Value, $fmt, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed)) {
            return $parsed.ToString('yyyy-MM-dd')
        }
    }
    throw "Could not parse date of birth '$Value' - expected dd/MM/yyyy or yyyy-MM-dd."
}

# Masks a NINO down to the last 4 characters for safer display/logging.
function Get-MaskedNino {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -le 4) { return $Value }
    return ('*' * ($Value.Length - 4)) + $Value.Substring($Value.Length - 4)
}

$rows = Import-Csv -Path $CsvPath
if (-not $rows -or $rows.Count -eq 0) {
    Write-Error "No rows found in $CsvPath."
    exit 1
}

foreach ($col in @('Name', 'LastName', 'DateOfBirth', 'NationalInsuranceNumber')) {
    if (-not ($rows[0].PSObject.Properties[$col])) {
        Write-Error "CSV must have a '$col' column."
        exit 1
    }
}

Write-Host "Found $($rows.Count) row(s) in $CsvPath." -ForegroundColor Cyan
Write-Host "Getting token..." -ForegroundColor Cyan
$token = Get-Token
Write-Host "Token acquired. Submitting checks..." -ForegroundColor Green

$headers = @{ Authorization = "Bearer $token" }
$guidPattern = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'
$results = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($row in $rows) {
    $maskedNino = Get-MaskedNino $row.NationalInsuranceNumber
    $result = [PSCustomObject]@{
        Name      = $row.Name
        Nino      = $maskedNino
        CheckId   = $null
        Status    = $null
        Tier      = $null
        ErrorCode = $null
    }

    try {
        $dob = ConvertTo-ApiDate $row.DateOfBirth

        $body = @{
            data = @{
                lastName                 = $row.LastName
                dateOfBirth              = $dob
                nationalInsuranceNumber  = $row.NationalInsuranceNumber
            }
        } | ConvertTo-Json

        $submitResponse = Invoke-RestMethod -Uri "$baseUrl/check/free-school-meals" -Method POST `
            -Headers $headers -ContentType "application/json" -Body $body

        $statusLink = $submitResponse.links.get_EligibilityCheckStatus
        if (-not $statusLink -and $submitResponse.links.get_EligibilityCheck) {
            $statusLink = "$($submitResponse.links.get_EligibilityCheck)/status"
        }
        if ($statusLink -match $guidPattern) {
            $result.CheckId = $Matches[0]
        }

        $status = $submitResponse.data.status

        # Check is processed asynchronously - poll until it moves off "queuedForProcessing".
        if ($result.CheckId) {
            $attempt = 0
            while ($status -eq 'queuedForProcessing' -and $attempt -lt $MaxPollAttempts) {
                Start-Sleep -Seconds $PollDelaySeconds
                $statusResponse = Invoke-RestMethod -Uri "$baseUrl/check/$($result.CheckId)/status" -Method GET -Headers $headers
                $status = $statusResponse.data.status
                $result.Tier = $statusResponse.data.tier
                $result.ErrorCode = $statusResponse.data.errorCode
                $attempt++
            }
        }

        $result.Status = $status
        if (-not $result.Tier) { $result.Tier = $submitResponse.data.tier }
        if (-not $result.ErrorCode) { $result.ErrorCode = $submitResponse.data.errorCode }

        $colour = switch ($status) {
            'eligible' { 'Green' }
            'notEligible' { 'Yellow' }
            'parentNotFound' { 'Yellow' }
            'queuedForProcessing' { 'DarkYellow' }
            default { 'Red' }
        }
        Write-Host "  $($row.Name): $status" -ForegroundColor $colour
    }
    catch {
        $result.Status = "ERROR: $($_.Exception.Message)"
        Write-Host "  $($row.Name): ERROR - $($_.Exception.Message)" -ForegroundColor Red
    }

    $results.Add($result)
}

Write-Host ""
$results | Format-Table -AutoSize

if ($OutputCsv) {
    $results | Export-Csv -Path $OutputCsv -NoTypeInformation
    Write-Host "Results written to $OutputCsv" -ForegroundColor Cyan
}
