<#
.SYNOPSIS
    ELIG-3610 Phase B: match a source CSV (parent/child identity + new email) against
    the Applications table by identity, WITHOUT changing any data.

.DESCRIPTION
    Runs entirely against the DB you point it at (default: Prod Read Replica) and never
    writes to Applications - it only creates a session-scoped #temp table to stage the
    CSV for a single set-based JOIN, then reports what it found.

    For each source CSV row, produces one of:
      - Unique   : exactly one matching Application found -> safe to update in Phase C.
      - Ambiguous: 2+ matching Applications found -> needs manual review, NOT auto-updated.
      - NoMatch  : 0 matching Applications found -> needs manual review, NOT auto-updated.

    Match key: ParentFirstName + ParentLastName + ParentDateOfBirth + cleaned NINO +
    ChildFirstName + ChildLastName + ChildDateOfBirth, restricted to Applications.Type =
    -ApplicationType (default FreeSchoolMeals). School Name is NOT part of the match key -
    it's only carried through as a manual sanity-check column (source vs matched school).

    IMPORTANT (data quality, confirmed against a real sample of this exact file):
      - "Date of birth" (parent) is UK format d/M/yyyy (e.g. 19/08/1990).
      - "ChildDOB" is US format M/d/yyyy (e.g. 3/19/2021).
      These are DIFFERENT formats in the same file - do not assume one shared format.
      Ambiguous dates (day AND month both <= 12) cannot be detected as mis-parsed from the
      format alone; this is a known limitation, not something this script can fully guard.
      Rows using the "Clean NI Number" column blank/NASS-only will never match (SQL NULL <>
      NULL) and will correctly fall out as NoMatch for manual review.

    Nothing containing real NINOs/emails is printed to the console - only row counts and
    classifications. The detailed report (written to -ReportOutputPath) DOES contain PII
    (NINO is not included, but names/DOB/email are) - handle it with the same care as the
    source CSV.

.PARAMETER CsvPath
    Path to the source CSV (columns: SchoolName, Clean School Name, First name, Last name,
    Date of birth, Clean NI Number, Contact email, ChildFirstName, ChildLastName, ChildDOB).

.PARAMETER ReportOutputPath
    Where to write the match report CSV. Defaults to "<CsvPath>.match-report.csv".

.PARAMETER Server
    SQL server. Defaults to the Prod Read Replica - do NOT point this at a writable
    database for Phase B; this script is match/report only.

.PARAMETER Database
    Database name. Defaults to "read-replica".

.PARAMETER Authentication
    SqlClient "Authentication" connection-string keyword. Defaults to
    "Active Directory Interactive" (opens a browser MFA prompt). Use
    "Active Directory Integrated" if your machine is domain-joined. Ignored if -UserId is
    supplied (SQL auth takes over), or if -ConnectionString is supplied (overrides everything).

.PARAMETER UserId
    Optional. SQL login username. If supplied (with -Password), builds a SQL-auth connection
    string instead of using -Authentication. You'll be prompted for -Password if you supply
    -UserId without it.

.PARAMETER Password
    Optional. SQL login password as a SecureString. Prompted for securely (masked) if -UserId
    is supplied and -Password is not - never type it as a plain -Password "literal" on the
    command line, since that would echo it into shell history.

.PARAMETER ConnectionString
    Optional. Overrides everything else (Server/Database/Authentication/UserId/Password) if
    supplied.

.PARAMETER ApplicationType
    Applications.Type to restrict matching to. Defaults to "FreeSchoolMeals".

.EXAMPLE
    .\Invoke-ParentEmailMatch.ps1 -CsvPath .\data\application-data-mini.csv

.EXAMPLE
    .\Invoke-ParentEmailMatch.ps1 -CsvPath .\data\application-data-mini.csv `
        -Server "ece-database.database.windows.net" -Database "read-replica"

.EXAMPLE
    # SQL auth - prompts securely for the password (masked input, never echoed)
    .\Invoke-ParentEmailMatch.ps1 -CsvPath .\data\dev-application-data.csv `
        -Server "ece-dev-database.database.windows.net" -Database "EligibilityCheck" `
        -UserId "my-sql-login"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [string]$ReportOutputPath,

    [string]$Server = "ece-database.database.windows.net",

    [string]$Database = "read-replica",

    [string]$Authentication = "Active Directory Interactive",

    [string]$UserId,

    [SecureString]$Password,

    [string]$ConnectionString,

    [string]$ApplicationType = "FreeSchoolMeals"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CsvPath)) {
    throw "CSV not found: $CsvPath"
}

if (-not $ReportOutputPath) {
    $ReportOutputPath = [System.IO.Path]::ChangeExtension($CsvPath, $null).TrimEnd('.') + ".match-report.csv"
}

Write-Host "Reading $CsvPath ..." -ForegroundColor Cyan
$sourceRows = Import-Csv -Path $CsvPath
Write-Host "  $($sourceRows.Count) source rows read." -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Build a typed DataTable matching the #Staging schema. Rows that fail to
# parse (bad/missing DOB) are excluded from staging and counted separately -
# they are never silently dropped without being reported.
# ---------------------------------------------------------------------------
$table = New-Object System.Data.DataTable
[void]$table.Columns.Add("RowNum", [int])
[void]$table.Columns.Add("ParentFirstName", [string])
[void]$table.Columns.Add("ParentLastName", [string])
[void]$table.Columns.Add("ParentDOB", [datetime])
[void]$table.Columns.Add("ParentNinoClean", [string])
[void]$table.Columns.Add("ChildFirstName", [string])
[void]$table.Columns.Add("ChildLastName", [string])
[void]$table.Columns.Add("ChildDOB", [datetime])
[void]$table.Columns.Add("SchoolName", [string])
[void]$table.Columns.Add("NewParentEmail", [string])

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$parseErrors = New-Object System.Collections.Generic.List[string]
$rowNum = 0

foreach ($row in $sourceRows) {
    $rowNum++
    try {
        $parentDob = [datetime]::ParseExact($row.'Date of birth'.Trim(), 'd/M/yyyy', $culture)
        $childDob = [datetime]::ParseExact($row.ChildDOB.Trim(), 'M/d/yyyy', $culture)
    }
    catch {
        $parseErrors.Add("Row $rowNum : DOB parse failed ($($_.Exception.Message))")
        continue
    }

    $ninoClean = ($row.'Clean NI Number' -replace '\s', '').Trim().ToUpperInvariant()
    $schoolName = if ($row.'Clean School Name') { $row.'Clean School Name' } else { $row.SchoolName }

    $dr = $table.NewRow()
    $dr.RowNum = $rowNum
    $dr.ParentFirstName = $row.'First name'.Trim()
    $dr.ParentLastName = $row.'Last name'.Trim()
    $dr.ParentDOB = $parentDob
    $dr.ParentNinoClean = $ninoClean
    $dr.ChildFirstName = $row.ChildFirstName.Trim()
    $dr.ChildLastName = $row.ChildLastName.Trim()
    $dr.ChildDOB = $childDob
    $dr.SchoolName = $schoolName
    $dr.NewParentEmail = $row.'Contact email'.Trim()
    [void]$table.Rows.Add($dr)
}

Write-Host "  $($table.Rows.Count) rows parsed OK, $($parseErrors.Count) rows failed to parse." -ForegroundColor Cyan
if ($parseErrors.Count -gt 0) {
    Write-Host "  (parse-error rows are excluded from matching - review them separately, no PII printed here)" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Connect, stage, match - all on ONE connection so the #temp table survives
# for the whole session. Read-only against Applications/Establishments.
# ---------------------------------------------------------------------------
if (-not $ConnectionString -and $UserId -and -not $Password) {
    $Password = Read-Host -Prompt "SQL password for '$UserId'" -AsSecureString
}

if (-not $ConnectionString) {
    if ($UserId) {
        # Decrypt only right before use; null out the plaintext copy immediately after.
        $plainPassword = [System.Net.NetworkCredential]::new('', $Password).Password
        $ConnectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;" +
            "User ID=$UserId;Password=$plainPassword;MultipleActiveResultSets=False;Encrypt=True;" +
            "TrustServerCertificate=False;Connection Timeout=30;"
        $plainPassword = $null
    }
    else {
        $ConnectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;" +
            "Authentication=$Authentication;MultipleActiveResultSets=False;Encrypt=True;" +
            "TrustServerCertificate=False;Connection Timeout=30;"
    }
}

$conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$sw = [System.Diagnostics.Stopwatch]::StartNew()

try {
    Write-Host "Connecting to $Server / $Database ..." -ForegroundColor Cyan
    $conn.Open()

    $createCmd = $conn.CreateCommand()
    $createCmd.CommandText = @"
CREATE TABLE #Staging (
    RowNum INT,
    ParentFirstName VARCHAR(100),
    ParentLastName VARCHAR(100),
    ParentDOB DATE,
    ParentNinoClean VARCHAR(50),
    ChildFirstName VARCHAR(50),
    ChildLastName VARCHAR(50),
    ChildDOB DATE,
    SchoolName VARCHAR(200),
    NewParentEmail VARCHAR(1000)
);
"@
    [void]$createCmd.ExecuteNonQuery()

    $bulk = New-Object System.Data.SqlClient.SqlBulkCopy($conn)
    $bulk.DestinationTableName = "#Staging"
    foreach ($col in $table.Columns) {
        [void]$bulk.ColumnMappings.Add($col.ColumnName, $col.ColumnName)
    }
    $bulk.WriteToServer($table)

    Write-Host "  Staged $($table.Rows.Count) rows. Running match query (Type='$ApplicationType')..." -ForegroundColor Cyan

    $matchCmd = $conn.CreateCommand()
    $matchCmd.CommandTimeout = 120
    $matchCmd.CommandText = @"
SELECT
    s.RowNum,
    s.ParentFirstName, s.ParentLastName, s.ChildFirstName, s.ChildLastName,
    s.NewParentEmail AS ProposedNewEmail,
    a.ApplicationID, a.Reference, a.ParentEmail AS CurrentParentEmail,
    s.SchoolName AS SourceSchool, e.EstablishmentName AS MatchedSchool,
    COUNT(a.ApplicationID) OVER (PARTITION BY s.RowNum) AS MatchCountForRow
FROM #Staging s
LEFT JOIN Applications a
    ON a.Type = @ApplicationType
    AND REPLACE(a.ParentNationalInsuranceNumber, ' ', '') = s.ParentNinoClean
    AND a.ParentDateOfBirth = s.ParentDOB
    AND a.ParentFirstName = s.ParentFirstName
    AND a.ParentLastName = s.ParentLastName
    AND a.ChildFirstName = s.ChildFirstName
    AND a.ChildLastName = s.ChildLastName
    AND a.ChildDateOfBirth = s.ChildDOB
LEFT JOIN Establishments e ON e.EstablishmentID = a.EstablishmentId
ORDER BY s.RowNum;
"@
    [void]$matchCmd.Parameters.AddWithValue("@ApplicationType", $ApplicationType)

    $reportTable = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $matchCmd
    [void]$adapter.Fill($reportTable)
}
finally {
    $conn.Close()
}

$sw.Stop()
Write-Host "  Match query completed in $($sw.ElapsedMilliseconds) ms." -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Classify and report. No PII to console - counts only.
# ---------------------------------------------------------------------------
[void]$reportTable.Columns.Add("Classification", [string])
[void]$reportTable.Columns.Add("SchoolMismatch", [bool])
foreach ($r in $reportTable.Rows) {
    $count = [int]$r["MatchCountForRow"]
    $r["Classification"] = if ($count -eq 1) { "Unique" } elseif ($count -eq 0) { "NoMatch" } else { "Ambiguous" }
    $r["SchoolMismatch"] = -not ([string]::Equals([string]$r["SourceSchool"], [string]$r["MatchedSchool"], [System.StringComparison]::OrdinalIgnoreCase))
}

$reportTable | Export-Csv -Path $ReportOutputPath -NoTypeInformation

$uniqueRows = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "Unique" })
$ambiguousRowNums = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "Ambiguous" } | Select-Object -ExpandProperty RowNum -Unique)
$noMatchRowNums = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "NoMatch" } | Select-Object -ExpandProperty RowNum -Unique)
$schoolMismatchCount = @($uniqueRows | Where-Object { $_["SchoolMismatch"] -eq $true }).Count

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Green
Write-Host "Source rows read:        $($sourceRows.Count)"
Write-Host "Parse errors (excluded): $($parseErrors.Count)"
Write-Host "Unique matches:          $($uniqueRows.Count)"
Write-Host "  of which school name mismatch (sanity-check flag): $schoolMismatchCount"
Write-Host "Ambiguous (2+ matches):  $($ambiguousRowNums.Count)"
Write-Host "No match (0 matches):    $($noMatchRowNums.Count)"
Write-Host "Report written to:       $ReportOutputPath"
Write-Host ""
Write-Host "Report contains PII (names/DOB/email) - handle with the same care as the source CSV." -ForegroundColor Yellow
