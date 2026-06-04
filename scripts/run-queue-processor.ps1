# run-queue-processor.ps1
# Simulates the Logic App that triggers queue processing in Azure.
# Polls all three bulk check queues every 5 seconds by calling POST /engine/process.
# Keep this running in a separate terminal while testing bulk checks locally.

$ApiBase = "https://localhost:7117"
$TokenEndpoint = "$ApiBase/oauth2/token"
$Queues = @(
    "process-eligibility-queue",
    "process-bulk-eligibility-queue",
    "process-bulk-wf-eligibility-queue"
)

# -SkipCertificateCheck handles the localhost dev certificate (PowerShell 7+)

function Get-Token {
    $body = @{
        client_id     = "Omni"
        client_secret = "OmniSecret"
        grant_type    = "client_credentials"
        scope         = "engine"
    }
    try {
        $response = Invoke-RestMethod -Uri $TokenEndpoint -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -SkipCertificateCheck
        return $response.access_token
    }
    catch {
        Write-Warning "Failed to get token: $_"
        return $null
    }
}

Write-Host "Starting queue processor. Press Ctrl+C to stop." -ForegroundColor Cyan
Write-Host "API: $ApiBase" -ForegroundColor DarkGray
Write-Host ""

$token = Get-Token
if (-not $token) {
    Write-Error "Could not get token from API. Is the API running at $ApiBase?"
    exit 1
}
Write-Host "Got token. Polling queues every 5 seconds..." -ForegroundColor Green

while ($true) {
    foreach ($queue in $Queues) {
        try {
            $headers = @{ Authorization = "Bearer $token" }
            $response = Invoke-RestMethod -Uri "$ApiBase/engine/process?queue=$queue" `
                -Method Post -Headers $headers -ContentType "application/json" -SkipCertificateCheck
            $msg = if ($response.data) { $response.data } else { "ok" }
            Write-Host "$(Get-Date -Format 'HH:mm:ss')  $queue => $msg" -ForegroundColor Gray
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 401) {
                Write-Host "$(Get-Date -Format 'HH:mm:ss')  Token expired, refreshing..." -ForegroundColor Yellow
                $token = Get-Token
                if (-not $token) { Write-Warning "Token refresh failed, will retry next cycle." }
            }
            elseif ($status -eq 400) {
                # Queue empty / invalid request — normal, not an error
                Write-Host "$(Get-Date -Format 'HH:mm:ss')  $queue => (empty)" -ForegroundColor DarkGray
            }
            else {
                Write-Warning "$(Get-Date -Format 'HH:mm:ss')  $queue => ERROR $status : $_"
            }
        }
    }
    Start-Sleep -Seconds 5
}
