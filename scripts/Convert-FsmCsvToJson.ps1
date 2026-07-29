<#
.SYNOPSIS
    Converts FSM bulk-check CSV files to JSON payloads for /bulk-check/free-school-meals.

.DESCRIPTION
    Reads one or more CSVs exported from Excel and produces one JSON file per CSV.
    Expected CSV columns (case-sensitive):
        Parent First Name, Parent Last Name, National Insurance Number,
        Parent Date of Birth, Child First Name, Child Last Name,
        Child date of birth, Child school URN

    Dates in the CSV must be in DD/MM/YYYY format (standard UK Excel export).
    The CSV must contain a 'client identifier' column whose value is used as-is.

.PARAMETER InputPath
    Path to a single CSV file, or a folder containing multiple CSVs.

.EXAMPLE
    .\Convert-FsmCsvToJson.ps1 -InputPath "C:\data\bulk"
    .\Convert-FsmCsvToJson.ps1 -InputPath "C:\data\bulk\school1.csv"
#>

param(
    [Parameter(Mandatory)]
    [string]$InputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve input to a list of CSV files
$csvFiles = if (Test-Path $InputPath -PathType Container) {
    Get-ChildItem -Path $InputPath -Filter '*.csv' | Sort-Object Name
} elseif (Test-Path $InputPath -PathType Leaf) {
    Get-Item $InputPath
} else {
    Write-Error "Path not found: $InputPath"
    exit 1
}

if (-not $csvFiles) {
    Write-Error "No CSV files found at: $InputPath"
    exit 1
}

foreach ($csvFile in $csvFiles) {
    Write-Host ""
    Write-Host "File: $($csvFile.Name)" -ForegroundColor Cyan

    # Prompt for per-file meta values
    $submittedBy     = Read-Host "  submittedBy (email)"
    $laIdInput       = Read-Host "  localAuthorityId (number)"
    $localAuthorityId = [int]$laIdInput

    $rows = Import-Csv -Path $csvFile.FullName

    if ($rows.Count -eq 0) {
        Write-Warning "  No data rows found — skipping."
        continue
    }

    $data = [System.Collections.Generic.List[object]]::new()

    foreach ($row in $rows) {
        # Parse dates — expected format DD/MM/YYYY
        try {
            $dob = [datetime]::ParseExact(
                $row.'Parent Date of Birth'.Trim(), 'dd/MM/yyyy', $null)
        } catch {
            Write-Warning "  Row with NI '$($row.'National Insurance Number')' — invalid Parent Date of Birth '$($row.'Parent Date of Birth')'. Skipping row."
            continue
        }

        $entry = [ordered]@{
            nationalInsuranceNumber = $row.'National Insurance Number'.Trim().ToUpper()
            lastName                = $row.'Parent Last Name'.Trim()
            dateOfBirth             = $dob.ToString('yyyy-MM-dd')
            clientIdentifier        = $row.'client identifier'.Trim()
        }

        $data.Add($entry)
    }

    $payload = [ordered]@{
        data = $data.ToArray()
        meta = [ordered]@{
            filename         = $csvFile.Name
            submittedBy      = $submittedBy
            localAuthorityId = $localAuthorityId
        }
    }

    $outputPath = Join-Path $csvFile.DirectoryName ($csvFile.BaseName + '.json')
    $payload | ConvertTo-Json -Depth 5 | Set-Content -Path $outputPath -Encoding UTF8NoBOM

    Write-Host "  -> $outputPath ($($data.Count) records)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done." -ForegroundColor Yellow
