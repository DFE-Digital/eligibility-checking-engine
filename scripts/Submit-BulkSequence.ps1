param(
    [string]$ApiBase,
    [string]$ClientId,
    [string]$ClientSecret,
    [int[]]$BatchCounts,
    [int]$RecordsPerBatch = 2500,
    [int]$WaitSeconds = 60,
    [string]$CheckType = 'free-school-meals',
    [ValidateSet('Tester', 'Random')]
    [string]$LastNameMode = 'Tester'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ApiBase) -or
    [string]::IsNullOrWhiteSpace($ClientId) -or
    [string]::IsNullOrWhiteSpace($ClientSecret)) {
    throw 'ApiBase, ClientId, and ClientSecret are required.'
}

if (-not $BatchCounts -or $BatchCounts.Count -eq 0) {
    throw 'Provide at least one batch count.'
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$simulateScript = Join-Path $scriptRoot 'simulate-bulk-load.ps1'

for ($index = 0; $index -lt $BatchCounts.Count; $index++) {
    $batchCount = $BatchCounts[$index]
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $outputPath = Join-Path $scriptRoot "dev-bulk-submissions-seq-$($batchCount)x$($RecordsPerBatch)-$timestamp.csv"

    Write-Host ([string]::Format('=== Sequence step {0}/{1}: submitting {2} batches ===', ($index + 1), $BatchCounts.Count, $batchCount)) -ForegroundColor Cyan

    & $simulateScript `
        -ApiBase $ApiBase `
        -ClientId $ClientId `
        -ClientSecret $ClientSecret `
        -CheckType $CheckType `
        -LastNameMode $LastNameMode `
        -NumberOfBatches $batchCount `
        -RecordsPerBatch $RecordsPerBatch `
        -SubmissionConcurrency $batchCount `
        -CreateOnly `
        -OutputPath $outputPath

    if ($index -lt ($BatchCounts.Count - 1)) {
        Write-Host "Waiting $WaitSeconds seconds before next submission wave..." -ForegroundColor Yellow
        Start-Sleep -Seconds $WaitSeconds
    }
}
