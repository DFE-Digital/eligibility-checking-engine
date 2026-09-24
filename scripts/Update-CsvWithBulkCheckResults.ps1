<#
.SYNOPSIS
    Updates an FSM bulk-check CSV with eligibility results from a Postman JSON response,
    or builds the eligible-only CSV straight from the results JSON when no source CSV exists.

.DESCRIPTION
    If -CsvPath is supplied, matches each JSON result to a CSV row using clientIdentifier
    (primary) or nationalInsuranceNumber (fallback). If -CsvPath is omitted, the CheckEligibilityItem
    fields already present in the results JSON (firstName, lastName, childFirstName, childLastName,
    childDateOfBirth, childSchoolURN, etc.) are used directly - this covers cases where you only have
    the bulk check ID/JSON and no LA-supplied CSV. Either way it:
      - Writes a new CSV ('_eligible.csv') containing only eligible rows, with columns matching
        ApplicationBulkImportRowMap so it can be submitted directly to /application/bulk-import.
        When multiple -JsonPath values are supplied, ALL their eligible rows are combined into a
        single eligible CSV.
      - Writes a tracker JSON file ('_tracker.json') summarising counts and listing the
        clientIdentifiers of not-eligible / unmatched rows. One tracker file is written PER SOURCE
        (per JSON file in JSON-only mode, or one for the CSV in CSV-matching mode) - trackers are
        never combined, even when the eligible CSV output is.

.PARAMETER CsvPath
    Optional. Path to the original LA-supplied CSV file. Omit this when you only have the
    results JSON (e.g. pulled from Postman via a bulk check ID) - rows will be built directly
    from the JSON instead. Only supported with a single -JsonPath file.

.PARAMETER JsonPath
    Path to one or more JSON files containing the Postman response body (the bulk check
    results, i.e. the 'data' array of CheckEligibilityItem). When -CsvPath is omitted, multiple
    -JsonPath values can be supplied - their eligible rows are combined into a single output CSV,
    but each JSON file still gets its own '_tracker.json'.

.PARAMETER OutputPath
    Optional. Base name (no extension) for the combined eligible CSV when multiple -JsonPath
    values are supplied. Defaults to 'combined'. Ignored when a single JSON file (or -CsvPath) is used.

.EXAMPLE
    .\Update-CsvWithBulkCheckResults.ps1 `
        -CsvPath  "C:\data\HCC Migration Eligible Batch 1 - Copy.csv" `
        -JsonPath "C:\data\results.json"

.EXAMPLE
    # No source CSV available - build the eligible-only CSV directly from the results JSON
    .\Update-CsvWithBulkCheckResults.ps1 -JsonPath "C:\data\batch1-results.json"

.EXAMPLE
    # Combine several results JSON files into a single eligible CSV, but keep one tracker per file
    .\Update-CsvWithBulkCheckResults.ps1 -JsonPath "C:\data\batch1.json", "C:\data\batch2.json" -OutputPath "combined"
#>

param(
    [string]$CsvPath,

    [Parameter(Mandatory)]
    [string[]]$JsonPath,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Load inputs ---------------------------------------------------------------

if ($CsvPath -and -not (Test-Path $CsvPath)) { Write-Error "CSV not found: $CsvPath";  exit 1 }

foreach ($jp in $JsonPath) {
    if (-not (Test-Path $jp)) { Write-Error "JSON not found: $jp"; exit 1 }
}

if ($CsvPath -and $JsonPath.Count -gt 1) {
    Write-Error "-CsvPath matching mode only supports a single -JsonPath file. Omit -CsvPath to combine multiple JSON result files."
    exit 1
}

$rows = if ($CsvPath) { Import-Csv -Path $CsvPath } else { $null }

# --- Helper: normalise a date value to yyyy-MM-dd ----------------------------
function Format-DateYMD ([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '' }
    # Strip time portion from ISO strings (e.g. 2027-07-31T00:00:00)
    $trimmed = $value.Trim() -replace 'T.*$', ''
    $formats = @('yyyy-MM-dd', 'dd/MM/yyyy', 'dd-MM-yyyy', 'MM/dd/yyyy', 'd/M/yyyy', 'd-M-yyyy')
    $parsed  = [datetime]::MinValue
    foreach ($fmt in $formats) {
        if ([datetime]::TryParseExact($trimmed, $fmt,
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::None,
                [ref]$parsed)) {
            return $parsed.ToString('yyyy-MM-dd')
        }
    }
    # Last-resort: let .NET try to infer the format
    if ([datetime]::TryParse($trimmed, [ref]$parsed)) {
        return $parsed.ToString('yyyy-MM-dd')
    }
    Write-Warning "Could not parse date: '$value'. Leaving as-is."
    return $value
}

# --- Helper: safely read a possibly-absent property (the API omits null --------
# properties from the JSON entirely, which throws under Set-StrictMode if
# accessed directly via dot notation).
function Get-Value ($obj, [string]$prop) {
    if ($obj.PSObject.Properties[$prop]) { return $obj.$prop } else { return $null }
}

# --- Helper: increment a tier tally, treating blank/missing tier as '(none)' ----
function Add-TierCount ($TierCounts, [string]$Tier) {
    $tierKey = if ([string]::IsNullOrWhiteSpace($Tier)) { '(none)' } else { $Tier }
    if ($TierCounts.Contains($tierKey)) {
        $TierCounts[$tierKey]++
    } else {
        $TierCounts[$tierKey] = 1
    }
}

# --- Helper: write a tracker JSON file for one source (CSV or single JSON file) -
function Write-TrackerFile {
    param(
        [string]$Path,
        [string]$SourceLabel,
        [int]$TotalRows,
        [int]$EligibleCount,
        $TierCounts,
        $NotEligibleIds,
        $ParentNotFoundIds,
        $ErrorIds,
        $NotFoundIds,
        $QueuedIds,
        $OtherIds,
        $UnmatchedIds
    )

    $tracker = [ordered]@{
        generatedAt          = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
        sourceFile           = $SourceLabel
        totalRows            = $TotalRows
        eligibleCount        = $EligibleCount
        notEligibleCount     = $NotEligibleIds.Count
        parentNotFoundCount  = $ParentNotFoundIds.Count
        errorCount           = $ErrorIds.Count
        notFoundCount        = $NotFoundIds.Count
        queuedCount          = $QueuedIds.Count
        otherCount           = $OtherIds.Count
        unmatchedCount       = $UnmatchedIds.Count
        tierCounts           = $TierCounts
        notEligible          = $NotEligibleIds.ToArray()
        parentNotFound       = $ParentNotFoundIds.ToArray()
        errors               = $ErrorIds.ToArray()
        notFound             = $NotFoundIds.ToArray()
        queued               = $QueuedIds.ToArray()
        other                = $OtherIds.ToArray()
        unmatched            = $UnmatchedIds.ToArray()
    }

    $tracker | ConvertTo-Json -Depth 3 | Set-Content -Path $Path -Encoding UTF8NoBOM
}

# --- Helper: print the console summary for one source ---------------------------
function Write-Summary {
    param(
        [string]$EligiblePath,
        [string]$TrackerPath,
        [int]$TotalRows,
        [int]$EligibleCount,
        $TierCounts,
        $NotEligibleIds,
        $ParentNotFoundIds,
        $ErrorIds,
        $NotFoundIds,
        $QueuedIds,
        $OtherIds,
        $UnmatchedIds
    )

    Write-Host ""
    if ($EligiblePath) { Write-Host "Eligible CSV : $EligiblePath" -ForegroundColor Green }
    Write-Host "Tracker file : $TrackerPath"  -ForegroundColor Green
    Write-Host ""
    Write-Host "  Total rows       : $TotalRows"
    Write-Host "  Eligible         : $EligibleCount"              -ForegroundColor Green
    foreach ($tierKey in $TierCounts.Keys) {
        Write-Host "    - tier '$tierKey' : $($TierCounts[$tierKey])" -ForegroundColor DarkGreen
    }
    Write-Host "  Not eligible     : $($NotEligibleIds.Count)"    -ForegroundColor Yellow
    if ($ParentNotFoundIds.Count -gt 0) {
        Write-Host "  Parent not found : $($ParentNotFoundIds.Count)" -ForegroundColor Yellow
    }
    if ($ErrorIds.Count -gt 0) {
        Write-Host "  Error            : $($ErrorIds.Count)"          -ForegroundColor Red
    }
    if ($NotFoundIds.Count -gt 0) {
        Write-Host "  Not found        : $($NotFoundIds.Count)"       -ForegroundColor Yellow
    }
    if ($QueuedIds.Count -gt 0) {
        Write-Host "  Still queued     : $($QueuedIds.Count)"         -ForegroundColor Cyan
    }
    if ($OtherIds.Count -gt 0) {
        Write-Host "  Other/unknown    : $($OtherIds.Count)"          -ForegroundColor Cyan
    }
    if ($UnmatchedIds.Count -gt 0) {
        Write-Host "  Unmatched        : $($UnmatchedIds.Count)"      -ForegroundColor Red
    }
}

# --- Match and update rows, writing one tracker per source, and a single --------
# combined eligible CSV across every JSON file supplied.

$allEligibleRows = [System.Collections.Generic.List[object]]::new()

if ($CsvPath) {
    # CSV matching mode: single CSV + single JSON, single tracker (matches source CSV).
    $jp   = $JsonPath[0]
    $data = (Get-Content $jp -Raw | ConvertFrom-Json).data
    if (-not $data) { Write-Error "No 'data' array found in JSON: $jp"; exit 1 }

    $byClientId = @{}
    $byNino     = @{}
    foreach ($r in $data) {
        $cid = (Get-Value $r 'clientIdentifier')?.ToString()?.Trim()
        $ni  = (Get-Value $r 'nationalInsuranceNumber')?.ToString()?.Trim()?.ToUpper()
        if ($cid) { $byClientId[$cid] = $r }
        if ($ni)  { $byNino[$ni]      = $r }
    }

    $notEligibleIds    = [System.Collections.Generic.List[string]]::new()
    $parentNotFoundIds = [System.Collections.Generic.List[string]]::new()
    $errorIds          = [System.Collections.Generic.List[string]]::new()
    $notFoundIds       = [System.Collections.Generic.List[string]]::new()
    $queuedIds         = [System.Collections.Generic.List[string]]::new()
    $otherIds          = [System.Collections.Generic.List[string]]::new()
    $unmatchedIds      = [System.Collections.Generic.List[string]]::new()
    $tierCounts        = [ordered]@{}

    foreach ($row in $rows) {
        $cid = $row.'client identifier'?.ToString()?.Trim()
        $ni  = $row.'National Insurance Number'?.ToString()?.Trim()?.ToUpper()

        $result = $null
        if ($cid -and $byClientId.ContainsKey($cid)) {
            $result = $byClientId[$cid]
        } elseif ($ni -and $byNino.ContainsKey($ni)) {
            $result = $byNino[$ni]
            Write-Warning "Row NI=$ni matched by NI number fallback (no clientIdentifier match)."
        }

        if (-not $result) {
            Write-Warning "No match found for clientIdentifier='$cid' / NI='$ni'."
            $unmatchedIds.Add($cid ?? $ni)
            continue
        }

        switch ($result.status) {
            'eligible' {
                $tier    = if ($result.PSObject.Properties['tier'])               { $result.tier }               else { '' }
                $endDate = if ($result.PSObject.Properties['eligibilityEndDate']) { $result.eligibilityEndDate } else { '' }

                $mappedRow = [pscustomobject][ordered]@{
                    'Parent First Name'    = $row.'Parent First Name'
                    'Parent Surname'       = $row.'Parent Last Name'
                    'Parent DOB'           = Format-DateYMD $row.'Parent Date of Birth'
                    'Parent Nino'          = $row.'National Insurance Number'
                    'Parent Email Address' = if ($row.PSObject.Properties['Parent Email Address']) { $row.'Parent Email Address' } else { '' }
                    'Child First Name'     = $row.'Child First Name'
                    'Child Surname'        = $row.'Child Last Name'
                    'Child Date of Birth'  = Format-DateYMD $row.'Child date of birth'
                    'Child School URN'     = $row.'Child school URN'
                    'Eligibility End Date' = Format-DateYMD $endDate
                    'Application Status'   = 'Entitled'
                    'tier'                 = $tier
                }
                $allEligibleRows.Add($mappedRow)
                Add-TierCount $tierCounts $tier
            }
            'notEligible'       { $notEligibleIds.Add($cid ?? $ni) }
            'parentNotFound'    { $parentNotFoundIds.Add($cid ?? $ni) }
            'error'             { $errorIds.Add($cid ?? $ni) }
            'notFound'          { $notFoundIds.Add($cid ?? $ni) }
            'queuedForProcessing' { $queuedIds.Add($cid ?? $ni) }
            default             { $otherIds.Add("$($cid ?? $ni) [$($result.status)]") }
        }
    }

    $sourceItem   = Get-Item $CsvPath
    $eligiblePath = Join-Path $sourceItem.DirectoryName ($sourceItem.BaseName + '_eligible.csv')
    $trackerPath  = Join-Path $sourceItem.DirectoryName ($sourceItem.BaseName + '_tracker.json')

    $allEligibleRows | Export-Csv -Path $eligiblePath -NoTypeInformation -Encoding UTF8NoBOM

    Write-TrackerFile -Path $trackerPath -SourceLabel $sourceItem.Name -TotalRows $rows.Count -EligibleCount $allEligibleRows.Count `
        -TierCounts $tierCounts -NotEligibleIds $notEligibleIds -ParentNotFoundIds $parentNotFoundIds -ErrorIds $errorIds `
        -NotFoundIds $notFoundIds -QueuedIds $queuedIds -OtherIds $otherIds -UnmatchedIds $unmatchedIds

    Write-Summary -EligiblePath $eligiblePath -TrackerPath $trackerPath -TotalRows $rows.Count -EligibleCount $allEligibleRows.Count `
        -TierCounts $tierCounts -NotEligibleIds $notEligibleIds -ParentNotFoundIds $parentNotFoundIds -ErrorIds $errorIds `
        -NotFoundIds $notFoundIds -QueuedIds $queuedIds -OtherIds $otherIds -UnmatchedIds $unmatchedIds
}
else {
    Write-Host "No -CsvPath supplied; building rows directly from each JSON file (one tracker per file)." -ForegroundColor Cyan

    foreach ($jp in $JsonPath) {
        $data = (Get-Content $jp -Raw | ConvertFrom-Json).data
        if (-not $data) {
            Write-Warning "No 'data' array found in JSON: $jp"
            continue
        }

        $notEligibleIds    = [System.Collections.Generic.List[string]]::new()
        $parentNotFoundIds = [System.Collections.Generic.List[string]]::new()
        $errorIds          = [System.Collections.Generic.List[string]]::new()
        $notFoundIds       = [System.Collections.Generic.List[string]]::new()
        $queuedIds         = [System.Collections.Generic.List[string]]::new()
        $otherIds          = [System.Collections.Generic.List[string]]::new()
        $unmatchedIds      = [System.Collections.Generic.List[string]]::new()
        $tierCounts        = [ordered]@{}
        $fileEligibleCount = 0

        foreach ($result in $data) {
            $cid = (Get-Value $result 'clientIdentifier')?.ToString()?.Trim()
            $ni  = (Get-Value $result 'nationalInsuranceNumber')?.ToString()?.Trim()?.ToUpper()
            $id  = $cid ?? $ni

            switch ($result.status) {
                'eligible' {
                    $tier    = Get-Value $result 'tier'
                    if (-not $tier) { $tier = '' }
                    $endDate = Get-Value $result 'eligibilityEndDate'
                    if (-not $endDate) { $endDate = '' }
                    $email   = Get-Value $result 'emailAddress'
                    if (-not $email) { $email = '' }

                    $mappedRow = [pscustomobject][ordered]@{
                        'Parent First Name'    = Get-Value $result 'firstName'
                        'Parent Surname'       = Get-Value $result 'lastName'
                        'Parent DOB'           = Format-DateYMD (Get-Value $result 'dateOfBirth')
                        'Parent Nino'          = Get-Value $result 'nationalInsuranceNumber'
                        'Parent Email Address' = $email
                        'Child First Name'     = Get-Value $result 'childFirstName'
                        'Child Surname'        = Get-Value $result 'childLastName'
                        'Child Date of Birth'  = Format-DateYMD (Get-Value $result 'childDateOfBirth')
                        'Child School URN'     = Get-Value $result 'childSchoolURN'
                        'Eligibility End Date' = Format-DateYMD $endDate
                        'Application Status'   = 'Entitled'
                        'tier'                 = $tier
                    }
                    $allEligibleRows.Add($mappedRow)
                    Add-TierCount $tierCounts $tier
                    $fileEligibleCount++
                }
                'notEligible'       { $notEligibleIds.Add($id) }
                'parentNotFound'    { $parentNotFoundIds.Add($id) }
                'error'             { $errorIds.Add($id) }
                'notFound'          { $notFoundIds.Add($id) }
                'queuedForProcessing' { $queuedIds.Add($id) }
                default             { $otherIds.Add("$id [$($result.status)]") }
            }
        }

        $jpItem      = Get-Item $jp
        $trackerPath = Join-Path $jpItem.DirectoryName ($jpItem.BaseName + '_tracker.json')

        Write-TrackerFile -Path $trackerPath -SourceLabel $jpItem.Name -TotalRows $data.Count -EligibleCount $fileEligibleCount `
            -TierCounts $tierCounts -NotEligibleIds $notEligibleIds -ParentNotFoundIds $parentNotFoundIds -ErrorIds $errorIds `
            -NotFoundIds $notFoundIds -QueuedIds $queuedIds -OtherIds $otherIds -UnmatchedIds $unmatchedIds

        Write-Host ""
        Write-Host "=== $($jpItem.Name) ===" -ForegroundColor Magenta
        Write-Summary -EligiblePath $null -TrackerPath $trackerPath -TotalRows $data.Count -EligibleCount $fileEligibleCount `
            -TierCounts $tierCounts -NotEligibleIds $notEligibleIds -ParentNotFoundIds $parentNotFoundIds -ErrorIds $errorIds `
            -NotFoundIds $notFoundIds -QueuedIds $queuedIds -OtherIds $otherIds -UnmatchedIds $unmatchedIds
    }

    # ---- Single combined eligible CSV across all JSON files --------------------
    $firstItem    = Get-Item $JsonPath[0]
    $baseName     = if ($JsonPath.Count -eq 1) { $firstItem.BaseName } elseif ($OutputPath) { $OutputPath } else { 'combined' }
    $eligiblePath = Join-Path $firstItem.DirectoryName ($baseName + '_eligible.csv')

    $allEligibleRows | Export-Csv -Path $eligiblePath -NoTypeInformation -Encoding UTF8NoBOM

    Write-Host ""
    Write-Host "=== Combined ===" -ForegroundColor Magenta
    Write-Host "Combined eligible CSV : $eligiblePath" -ForegroundColor Green
    Write-Host "  Eligible rows across all $($JsonPath.Count) file(s) : $($allEligibleRows.Count)" -ForegroundColor Green
}
