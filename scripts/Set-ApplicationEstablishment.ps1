# Set-ApplicationEstablishment.ps1
# Reads a CSV with "Reference" and "URN New School" columns and PATCHes
# /application/reference/{reference} for each row.
# Retrieves a bearer token first using client credentials.

param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath
)

$baseUrl = $env:CYE_API_BASE_URL
$clientId = $env:CYE_CLIENT_ID
$clientSecret = $env:CYE_CLIENT_SECRET
$scope = "local_authority check application admin bulk_check establishment user engine notification free_school_meals two_year_offer early_year_pupil_premium working_families multi_academy_trust"

if ([string]::IsNullOrWhiteSpace($baseUrl) -or
    [string]::IsNullOrWhiteSpace($clientId) -or
    [string]::IsNullOrWhiteSpace($clientSecret)) {
    Write-Error "Set CYE_API_BASE_URL, CYE_CLIENT_ID and CYE_CLIENT_SECRET environment variables before running this script."
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

# --- Main ---
if (-not (Test-Path $CsvPath)) {
    Write-Error "CSV file not found: $CsvPath"
    exit 1
}

$rows = Import-Csv -Path $CsvPath

# Support both "URN New School" (from Excel export) and plain "URN"
$sampleRow = $rows | Select-Object -First 1
$urnColumn = if ($sampleRow.PSObject.Properties["URN New School"]) { "URN New School" }
             elseif ($sampleRow.PSObject.Properties["URN"]) { "URN" }
             else {
                 Write-Error "CSV must have a 'URN New School' or 'URN' column."
                 exit 1
             }

Write-Host "Found $($rows.Count) row(s). URN column: '$urnColumn'" -ForegroundColor Cyan
Write-Host "Getting token..." -ForegroundColor Cyan
$token = Get-Token
Write-Host "Token acquired. Patching applications..." -ForegroundColor Green

$headers = @{ Authorization = "Bearer $token" }
$success = 0
$failure = 0

foreach ($row in $rows) {
    $reference = $row.Reference
    $urn = $row.$urnColumn

    if (-not $reference -or -not $urn) {
        Write-Warning "  SKIP  row with empty Reference or URN (Reference='$reference', URN='$urn')"
        continue
    }

    $body = @{
        data = @{
            EstablishmentUrn = [int]$urn
        }
    } | ConvertTo-Json

    $url = "$baseUrl/application/reference/$reference"
    try {
        Invoke-RestMethod -Uri $url -Method Patch -Headers $headers `
            -ContentType "application/json" -Body $body | Out-Null
        Write-Host "  OK    Reference=$reference  URN=$urn" -ForegroundColor Green
        $success++
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "  FAIL  Reference=$reference  URN=$urn  (HTTP $status) $_" -ForegroundColor Red
        $failure++
    }
}

Write-Host ""
Write-Host "Done. $success succeeded, $failure failed." -ForegroundColor Cyan
