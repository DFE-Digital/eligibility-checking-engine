<#
.SYNOPSIS
    Searches a folder of CSV files for records matching a supplied list of people.

.DESCRIPTION
    Reads an input CSV with columns:
        Reference, ParentNationalInsuranceNumber, ParentFirstName, ParentLastName,
        ChildFirstName, ChildLastName
    Matches are found when ALL five non-Reference fields match a row in the search CSVs
    (case-insensitive). Reference is passed through to the output only.

.PARAMETER InputCsvPath
    Path to the input CSV containing the records to search for.

.PARAMETER CsvFolder
    Path to the folder containing the CSV files to search.

.PARAMETER OutputPath
    (Optional) Path for the results CSV. Defaults to <CsvFolder>\search-results.csv.

.EXAMPLE
    .\Find-NiNumbersInCsvs.ps1 `
        -InputCsvPath "C:\data\people.csv" `
        -CsvFolder    "C:\data\herts-csvs"
#>

param(
    [Parameter(Mandatory)]
    [string]$InputCsvPath,

    [Parameter(Mandatory)]
    [string]$CsvFolder,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Validate inputs ----------------------------------------------------------

if (-not (Test-Path $InputCsvPath)) { Write-Error "Input CSV not found: $InputCsvPath"; exit 1 }
if (-not (Test-Path $CsvFolder -PathType Container)) { Write-Error "CSV folder not found: $CsvFolder"; exit 1 }

if (-not $OutputPath) {
    $OutputPath = Join-Path $CsvFolder 'search-results.csv'
}

# --- Load input records -------------------------------------------------------

$inputRows = Import-Csv -Path $InputCsvPath

if ($inputRows.Count -eq 0) {
    Write-Error "No records found in: $InputCsvPath"
    exit 1
}

# Helper: build a normalised composite key from the five match fields
function Get-CompositeKey($ni, $parentFirst, $parentLast, $childFirst, $childLast) {
    return ("$ni|$parentFirst|$parentLast|$childFirst|$childLast").ToUpper().Trim()
}

# Build a hashtable for O(1) lookup: compositeKey -> input row
$lookup = @{}
foreach ($r in $inputRows) {
    $key = Get-CompositeKey `
        $r.ParentNationalInsuranceNumber `
        $r.ParentFirstName `
        $r.ParentLastName `
        $r.ChildFirstName `
        $r.ChildLastName
    # Store the input row; use a list to handle accidental duplicates in input
    if (-not $lookup.ContainsKey($key)) {
        $lookup[$key] = [System.Collections.Generic.List[object]]::new()
    }
    $lookup[$key].Add($r)
}

# Track which keys were matched
$matchResults = @{}
foreach ($key in $lookup.Keys) { $matchResults[$key] = [System.Collections.Generic.List[object]]::new() }

Write-Host ""
Write-Host "Records to search : $($inputRows.Count)" -ForegroundColor Cyan

# --- Search CSV files ---------------------------------------------------------

$csvFiles = Get-ChildItem -Path $CsvFolder -Filter '*.csv' |
    Where-Object { $_.FullName -ne $OutputPath }   # don't search the output file itself

if ($csvFiles.Count -eq 0) {
    Write-Error "No CSV files found in: $CsvFolder"
    exit 1
}

Write-Host "CSV files to search  : $($csvFiles.Count)" -ForegroundColor Cyan
Write-Host ""

foreach ($csvFile in $csvFiles) {
    Write-Host "Scanning: $($csvFile.Name)" -ForegroundColor Gray

    $rows = Import-Csv -Path $csvFile.FullName
    $rowIndex = 1

    foreach ($row in $rows) {
        $key = Get-CompositeKey `
            ($row.PSObject.Properties['National Insurance Number']?.Value) `
            ($row.PSObject.Properties['Parent First Name']?.Value) `
            ($row.PSObject.Properties['Parent Last Name']?.Value) `
            ($row.PSObject.Properties['Child First Name']?.Value) `
            ($row.PSObject.Properties['Child Last Name']?.Value)

        if ($matchResults.ContainsKey($key)) {
            $schoolName = $row.PSObject.Properties['Child school ']?.Value ??
                          $row.PSObject.Properties['School Name']?.Value

            # Retrieve Reference from the original input row
            $inputRow   = $lookup[$key][0]

            $matchResults[$key].Add([ordered]@{
                Reference                     = $inputRow.Reference
                ParentNationalInsuranceNumber = $row.PSObject.Properties['National Insurance Number']?.Value
                ParentFirstName               = $row.PSObject.Properties['Parent First Name']?.Value
                ParentLastName                = $row.PSObject.Properties['Parent Last Name']?.Value
                ChildFirstName                = $row.PSObject.Properties['Child First Name']?.Value
                ChildLastName                 = $row.PSObject.Properties['Child Last Name']?.Value
                Found                         = 'Yes'
                SourceFile                    = $csvFile.Name
                RowNumber                     = $rowIndex
                ParentDateOfBirth             = $row.PSObject.Properties['Parent Date of Birth']?.Value
                ChildDateOfBirth              = $row.PSObject.Properties['Child date of birth']?.Value
                ChildSchoolURN                = $row.PSObject.Properties['Child school URN']?.Value
                SchoolName                    = $schoolName
                ClientIdentifier              = $row.PSObject.Properties['client identifier']?.Value
            })
        }

        $rowIndex++
    }
}

# --- Build results ------------------------------------------------------------

$results = [System.Collections.Generic.List[object]]::new()
$foundCount    = 0
$notFoundCount = 0

foreach ($inputRow in $inputRows) {
    $key = Get-CompositeKey `
        $inputRow.ParentNationalInsuranceNumber `
        $inputRow.ParentFirstName `
        $inputRow.ParentLastName `
        $inputRow.ChildFirstName `
        $inputRow.ChildLastName

    $hits = $matchResults[$key]

    if ($hits -and $hits.Count -gt 0) {
        foreach ($hit in $hits) { $results.Add($hit) }
        $foundCount++
    } else {
        $results.Add([ordered]@{
            Reference                     = $inputRow.Reference
            ParentNationalInsuranceNumber = $inputRow.ParentNationalInsuranceNumber
            ParentFirstName               = $inputRow.ParentFirstName
            ParentLastName                = $inputRow.ParentLastName
            ChildFirstName                = $inputRow.ChildFirstName
            ChildLastName                 = $inputRow.ChildLastName
            Found                         = 'No'
            SourceFile                    = ''
            RowNumber                     = ''
            ParentDateOfBirth             = ''
            ChildDateOfBirth              = ''
            ChildSchoolURN                = ''
            SchoolName                    = ''
            ClientIdentifier              = ''
        })
        $notFoundCount++
    }
}

# --- Write output -------------------------------------------------------------

$results | ForEach-Object { [PSCustomObject]$_ } |
    Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8NoBOM

# --- Summary ------------------------------------------------------------------

Write-Host ""
Write-Host "Results written to: $OutputPath" -ForegroundColor Green
Write-Host ""
Write-Host "  Found     : $foundCount"    -ForegroundColor Green
Write-Host "  Not found : $notFoundCount" -ForegroundColor Yellow

if ($notFoundCount -gt 0) {
    Write-Host ""
    Write-Host "Records not found in any CSV:" -ForegroundColor Yellow
    foreach ($inputRow in $inputRows) {
        $key = Get-CompositeKey `
            $inputRow.ParentNationalInsuranceNumber `
            $inputRow.ParentFirstName `
            $inputRow.ParentLastName `
            $inputRow.ChildFirstName `
            $inputRow.ChildLastName
        if ($matchResults[$key].Count -eq 0) {
            Write-Host "  $($inputRow.ParentNationalInsuranceNumber) | $($inputRow.ParentFirstName) $($inputRow.ParentLastName) | $($inputRow.ChildFirstName) $($inputRow.ChildLastName)" -ForegroundColor Yellow
        }
    }
}
