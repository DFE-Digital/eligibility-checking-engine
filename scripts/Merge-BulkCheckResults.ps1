# ============================================================
# Merge-BulkCheckResults.ps1
# Combines and aggregates all CSV outputs from
# Check-BulkCheckProgress.ps1 runs into a single holistic view.
#
# Reads from $inputDir and writes merged files back there.
# ============================================================

$inputDir  = "C:\Test Projects\Investigation"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

# ============================================================
# 1. INCOMPLETE BULK CHECKS — combine + deduplicate by BulkCheckID
#    (if the same ID appears in multiple runs, keep the latest run's data)
# ============================================================

$incompleteFiles = Get-ChildItem -Path $inputDir -Filter "incomplete-bulk-checks-*.csv" | Sort-Object LastWriteTime

if ($incompleteFiles.Count -eq 0) {
    Write-Host "No incomplete-bulk-checks-*.csv files found in $inputDir" -ForegroundColor Yellow
}
else {
    Write-Host "Found $($incompleteFiles.Count) incomplete-bulk-checks file(s)." -ForegroundColor Cyan

    # Import all, tag with source file, sort by file date so later runs win dedup
    $allIncomplete = $incompleteFiles | ForEach-Object {
        $file = $_
        Import-Csv -Path $file.FullName | ForEach-Object {
            $_ | Add-Member -NotePropertyName _FileDate -NotePropertyValue $file.LastWriteTime -PassThru
        }
    }

    # Deduplicate: for each BulkCheckID keep the record from the most recent file
    $deduped = $allIncomplete |
        Sort-Object BulkCheckID, _FileDate |
        Group-Object BulkCheckID |
        ForEach-Object { $_.Group | Select-Object -Last 1 } |
        Select-Object -ExcludeProperty _FileDate |
        Sort-Object SubmittedDate -Descending

    $mergedIncompletePath = Join-Path $inputDir "MERGED-incomplete-bulk-checks_$timestamp.csv"
    $deduped | Export-Csv -Path $mergedIncompletePath -NoTypeInformation

    Write-Host ""
    Write-Host "===== INCOMPLETE BULK CHECKS (deduplicated) =====" -ForegroundColor Yellow
    Write-Host "  Total unique incomplete batches : $($deduped.Count)" -ForegroundColor Red
    Write-Host "  Exported to: $mergedIncompletePath" -ForegroundColor Cyan
    $deduped | Select-Object -First 10 | Format-Table SubmittedDate, BulkCheckID, Total, Complete, Remaining -AutoSize
    if ($deduped.Count -gt 10) {
        Write-Host "  ... and $($deduped.Count - 10) more (see CSV for full list)" -ForegroundColor DarkGray
    }
}

# ============================================================
# 2. HOURLY SUMMARY — combine all runs, re-aggregate by Hour_UTC
# ============================================================

$hourlyFiles = Get-ChildItem -Path $inputDir -Filter "hourly-summary-*.csv" | Sort-Object LastWriteTime

if ($hourlyFiles.Count -eq 0) {
    Write-Host "No hourly-summary-*.csv files found in $inputDir" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "Found $($hourlyFiles.Count) hourly-summary file(s)." -ForegroundColor Cyan

    $allHourly = $hourlyFiles | ForEach-Object { Import-Csv -Path $_.FullName }

    # Re-aggregate: sum Complete + Incomplete per Hour_UTC across all files
    $aggregated = $allHourly |
        Group-Object Hour_UTC |
        ForEach-Object {
            $hour  = $_.Name
            $c     = ($_.Group | Measure-Object Complete   -Sum).Sum
            $inc   = ($_.Group | Measure-Object Incomplete -Sum).Sum
            $tot   = $c + $inc
            [PSCustomObject]@{
                Hour_UTC       = $hour
                Complete       = $c
                Incomplete     = $inc
                Total          = $tot
                "Complete_%"   = if ($tot -gt 0) { [math]::Round($c   / $tot * 100, 1) } else { 0 }
                "Incomplete_%" = if ($tot -gt 0) { [math]::Round($inc / $tot * 100, 1) } else { 0 }
            }
        } | Sort-Object Hour_UTC

    $mergedHourlyPath = Join-Path $inputDir "MERGED-hourly-summary_$timestamp.csv"
    $aggregated | Export-Csv -Path $mergedHourlyPath -NoTypeInformation

    Write-Host ""
    Write-Host "===== HOURLY SUMMARY (all runs aggregated) =====" -ForegroundColor Yellow
    $aggregated | Format-Table -AutoSize
    Write-Host "  Exported to: $mergedHourlyPath" -ForegroundColor Cyan

    # ASCII chart
    Write-Host ""
    Write-Host "  Legend: " -NoNewline
    Write-Host "█ Complete " -ForegroundColor Green -NoNewline
    Write-Host "█ Incomplete" -ForegroundColor Red
    Write-Host ""

    $maxCount = ($aggregated | Measure-Object Total -Maximum).Maximum
    $barWidth = 50

    foreach ($row in $aggregated) {
        $cBar  = if ($maxCount -gt 0) { [math]::Round($row.Complete   / $maxCount * $barWidth) } else { 0 }
        $iBar  = if ($maxCount -gt 0) { [math]::Round($row.Incomplete / $maxCount * $barWidth) } else { 0 }
        Write-Host -NoNewline ("  {0}  " -f $row.Hour_UTC)
        Write-Host -NoNewline ("█" * $cBar)  -ForegroundColor Green
        Write-Host -NoNewline ("█" * $iBar)  -ForegroundColor Red
        Write-Host ("  ($($row.Complete) / $($row.Total))")
    }
    Write-Host ""
    Write-Host "  (count shown as: complete / total per hour)" -ForegroundColor DarkGray
}

# ============================================================
# 3. OVERALL SUMMARY
# ============================================================

if ($incompleteFiles.Count -gt 0 -and $hourlyFiles.Count -gt 0) {
    $grandTotal    = ($aggregated | Measure-Object Total    -Sum).Sum
    $grandComplete = ($aggregated | Measure-Object Complete -Sum).Sum
    $grandInc      = ($aggregated | Measure-Object Incomplete -Sum).Sum

    Write-Host ""
    Write-Host "===== OVERALL SUMMARY =====" -ForegroundColor Yellow
    Write-Host "  Total bulk checks examined : $grandTotal"
    Write-Host "  Complete                   : $grandComplete ($([math]::Round($grandComplete / $grandTotal * 100, 1))%)" -ForegroundColor Green
    Write-Host "  Incomplete                 : $grandInc ($([math]::Round($grandInc / $grandTotal * 100, 1))%)" -ForegroundColor Red
    Write-Host "  Unique incomplete (deduped): $($deduped.Count)" -ForegroundColor Red
}
