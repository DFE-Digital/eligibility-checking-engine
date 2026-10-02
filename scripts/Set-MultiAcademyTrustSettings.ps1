# Set-MultiAcademyTrustSettings.ps1
# PATCHes /multi-academy-trusts/{id}/settings for each ID in the $ids array.
# Retrieves a bearer token first using client credentials.

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

# --- IDs to update ---
$ids = @(
    2088,2111,2140,2157,2189,2209,2267,2288,2317,2345,2481,2510,2543,2624,2635,2661,2802,2842,2886,2928,3025,3107,3136,3152,3161,3190,3220,3259,3320,3342,3402,3433,3434,3437,3593,3637,3662,3663,3789,3865,3926,4043,4076,4100,4106,4154,4206,4235,4258,4289,4301,4317,4320,4353,4356,4403,4484,4528,4533,4578,4686,4739,4740,4751,4816,4836,4873,5143,5230,5231,5271,5291,5295,5296,5409,5538,5616,5663,15835,15838,15905,16254,16339,16469,16470,16578,16591,16639,16653,16752,16754,16768,16797,16895,16897,16899,17068,17202,17214,17338,17357,17441,17469,17524,17539,17874
)

# --- Payload ---
$body = @{
    AcademyCanReviewEvidence = $true
} | ConvertTo-Json

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
if ($ids.Count -eq 0) {
    Write-Warning "No IDs defined. Add IDs to the `$ids array and re-run."
    exit 0
}

Write-Host "Getting token..." -ForegroundColor Cyan
$token = Get-Token
Write-Host "Token acquired. Patching $($ids.Count) MAT(s)..." -ForegroundColor Green

$headers = @{ Authorization = "Bearer $token" }
$success = 0
$failure = 0

foreach ($id in $ids) {
    $url = "$baseUrl/multi-academy-trusts/$id/settings"
    try {
        $response = Invoke-RestMethod -Uri $url -Method Patch -Headers $headers `
            -ContentType "application/json" -Body $body
        Write-Host "  OK  $id" -ForegroundColor Green
        $success++
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "  FAIL $id  (HTTP $status) $_" -ForegroundColor Red
        $failure++
    }
}

Write-Host ""
Write-Host "Done. $success succeeded, $failure failed." -ForegroundColor Cyan
