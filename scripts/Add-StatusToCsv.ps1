<#
.SYNOPSIS
    Adds Status and Tier columns to the original FSM bulk-check CSV using the
    bulk-check results JSON and/or the tracker JSON.

.DESCRIPTION
    Reads the original CSV (which must contain a 'client identifier' column) and
    matches each row against the results JSON (primary) and/or the tracker JSON
    (fallback), then appends 'Status' and 'Tier' columns.

    At least one of -JsonPath or -TrackerPath must be supplied.

    Status values: eligible | notEligible | parentNotFound | error |
                   notFound | queuedForProcessing | other | unmatched |
                   'Check if last name, NINo and DoB are valid'

    Tier is populated only for eligible rows (from the results JSON).

    Writes a new CSV alongside the original named '<original>_with_status.csv'.

.PARAMETER CsvPath
    Path to the original CSV file containing the 'client identifier' column.

.PARAMETER JsonPath
    Path to the bulk-check results JSON (the Postman response body used by
    Update-CsvWithBulkCheckResults.ps1). Provides status AND tier for every row.

.PARAMETER TrackerPath
    Path to the tracker JSON produced by Update-CsvWithBulkCheckResults.ps1.
    Used as a fallback when JsonPath is not supplied, or to catch any rows the
    results JSON does not cover. Does not supply tier information.

.PARAMETER OutputPath
    Optional. Full path for the output CSV. Defaults to the same folder as CsvPath
    with '_with_status' appended to the base name.

.EXAMPLE
    # Richest output — status + tier from results JSON:
    .\Add-StatusToCsv.ps1 `
        -CsvPath  "C:\data\HCC Migration Eligible Batch 7.csv" `
        -JsonPath "C:\data\Results\HCC Migration Eligible Batch 7.json"

.EXAMPLE
    # Status only from tracker (no tier):
    .\Add-StatusToCsv.ps1 `
        -CsvPath     "C:\data\HCC Migration Sent for Review.csv" `
        -TrackerPath "C:\data\HCC Migration Sent for Review_tracker.json"

.EXAMPLE
    # Both sources — JSON for status+tier, tracker as fallback:
    .\Add-StatusToCsv.ps1 `
        -CsvPath     "C:\data\HCC Migration Eligible Batch 7.csv" `
        -JsonPath    "C:\data\Results\HCC Migration Eligible Batch 7.json" `
        -TrackerPath "C:\data\HCC Migration Eligible Batch 7_tracker.json"
#>

param(
    [Parameter(Mandatory)]
    [string]$CsvPath,

    [string]$JsonPath,

    [string]$TrackerPath,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $JsonPath -and -not $TrackerPath) {
    Write-Error "Supply at least one of -JsonPath or -TrackerPath."
    exit 1
}

# --- Validate inputs ----------------------------------------------------------

if (-not (Test-Path $CsvPath)) { Write-Error "CSV not found: $CsvPath"; exit 1 }
if ($JsonPath    -and -not (Test-Path $JsonPath))    { Write-Error "Results JSON not found: $JsonPath";  exit 1 }
if ($TrackerPath -and -not (Test-Path $TrackerPath)) { Write-Error "Tracker not found: $TrackerPath";    exit 1 }

$rows = Import-Csv -Path $CsvPath

if ($rows.Count -eq 0) {
    Write-Warning "No data rows found in CSV — nothing to do."
    exit 0
}

# --- Build lookup from results JSON (status + tier) ---------------------------
# Key: clientIdentifier (primary), nationalInsuranceNumber (secondary fallback)

$jsonLookupByCid  = @{}
$jsonLookupByNino = @{}

if ($JsonPath) {
    $results = (Get-Content $JsonPath -Raw | ConvertFrom-Json).data
    if (-not $results) { Write-Error "No 'data' array found in results JSON." ; exit 1 }

    foreach ($r in $results) {
        $cid  = $r.clientIdentifier?.ToString().Trim()
        $nino = $r.nationalInsuranceNumber?.ToString().Trim().ToUpper()
        $entry = @{
            status = $r.status
            tier   = if ($r.PSObject.Properties['tier'])  { $r.tier }  else { '' }
        }
        if ($cid)  { $jsonLookupByCid[$cid]   = $entry }
        if ($nino) { $jsonLookupByNino[$nino]  = $entry }
    }
    Write-Host "Loaded $($results.Count) results from results JSON." -ForegroundColor Cyan
}

# --- Build fallback lookup from tracker arrays (status only, no tier) ---------

$trackerStatusLookup = @{}

if ($TrackerPath) {
    $tracker = Get-Content $TrackerPath -Raw | ConvertFrom-Json

    $trackerMappings = [ordered]@{
        notEligible    = 'notEligible'
        parentNotFound = 'parentNotFound'
        errors         = 'error'
        notFound       = 'notFound'
        queued         = 'queuedForProcessing'
        other          = 'other'
        unmatched      = 'unmatched'
    }

    foreach ($property in $trackerMappings.Keys) {
        $label = $trackerMappings[$property]
        $ids   = $tracker.$property
        if ($ids) {
            foreach ($id in $ids) { $trackerStatusLookup[$id.ToString().Trim()] = $label }
        }
    }
    Write-Host "Loaded tracker: $($trackerStatusLookup.Count) non-eligible entries." -ForegroundColor Cyan
}

# --- Annotate each CSV row ----------------------------------------------------

$outputRows = [System.Collections.Generic.List[object]]::new()

$counts = @{
    eligible            = 0
    notEligible         = 0
    parentNotFound      = 0
    error               = 0
    notFound            = 0
    queuedForProcessing = 0
    other               = 0
    unmatched           = 0
'Check if last name, NINo and DoB are valid' = 0
}

foreach ($row in $rows) {
    $cid  = $row.'client identifier'?.ToString().Trim()
    $nino = $row.'National Insurance Number'?.ToString().Trim().ToUpper()

    $status = ''
    $tier   = ''

    # 1. Try results JSON by clientIdentifier
    if ($cid -and $jsonLookupByCid.ContainsKey($cid)) {
        $match  = $jsonLookupByCid[$cid]
        $status = $match.status
        $tier   = $match.tier
    }
    # 2. Try results JSON by NI number (fallback)
    elseif ($nino -and $jsonLookupByNino.ContainsKey($nino)) {
        $match  = $jsonLookupByNino[$nino]
        $status = $match.status
        $tier   = $match.tier
        Write-Warning "Row clientIdentifier='$cid' matched by NI fallback in results JSON."
    }
    # 3. Try tracker fallback (status only)
    elseif ($cid -and $trackerStatusLookup.ContainsKey($cid)) {
        $status = $trackerStatusLookup[$cid]
        $tier   = ''
    }
    # 4. Not in any negative list and not unmatched — infer eligible
    elseif ($cid -and $TrackerPath) {
        $status = 'eligible'
        $tier   = ''
    }
    # 5. No tracker to infer from and not in JSON
    elseif ($cid) {
        Write-Warning "Row clientIdentifier='$cid' not found in any source."
        $status = 'Check if last name, NINo and DoB are valid'
    }
    else {
        Write-Warning "Row has no client identifier."
        $status = 'Check if last name, NINo and DoB are valid'
    }

    # Build output row: copy all existing columns, then append Status and Tier
    $props = [ordered]@{}
    foreach ($col in $row.PSObject.Properties) { $props[$col.Name] = $col.Value }
    $props['Status'] = $status
    $props['Tier']   = $tier

    $outputRows.Add([pscustomobject]$props)

    $key = if ($counts.ContainsKey($status)) { $status } else { 'Check if last name, NINo and DoB are valid' }
    $counts[$key]++
}

# --- Write output CSV ---------------------------------------------------------

$csvItem = Get-Item $CsvPath

if (-not $OutputPath) {
    $OutputPath = Join-Path $csvItem.DirectoryName ($csvItem.BaseName + '_with_status' + $csvItem.Extension)
}

$outputRows | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8NoBOM

# --- Summary ------------------------------------------------------------------

Write-Host ""
Write-Host "Output CSV : $OutputPath" -ForegroundColor Green
Write-Host ""
Write-Host "  Total rows            : $($rows.Count)"
Write-Host "  Eligible              : $($counts['eligible'])"            -ForegroundColor Green
Write-Host "  Not eligible          : $($counts['notEligible'])"         -ForegroundColor Yellow
if ($counts['parentNotFound']      -gt 0) { Write-Host "  Parent not found      : $($counts['parentNotFound'])"      -ForegroundColor Yellow }
if ($counts['error']               -gt 0) { Write-Host "  Error                 : $($counts['error'])"               -ForegroundColor Red    }
if ($counts['notFound']            -gt 0) { Write-Host "  Not found             : $($counts['notFound'])"            -ForegroundColor Yellow }
if ($counts['queuedForProcessing'] -gt 0) { Write-Host "  Queued for processing : $($counts['queuedForProcessing'])" -ForegroundColor Cyan   }
if ($counts['other']               -gt 0) { Write-Host "  Other/unknown status  : $($counts['other'])"              -ForegroundColor Cyan   }
if ($counts['unmatched']           -gt 0) { Write-Host "  Unmatched             : $($counts['unmatched'])"           -ForegroundColor Red    }
if ($counts['Check if last name, NINo and DoB are valid'] -gt 0) { Write-Host "  Check name/NINo/DoB   : $($counts['Check if last name, NINo and DoB are valid'])" -ForegroundColor Red }
