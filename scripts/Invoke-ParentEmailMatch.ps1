<#
.SYNOPSIS
    Match a corrected parent-email CSV against imported Applications, WITHOUT changing any data.

.DESCRIPTION
    Runs entirely against the DB you point it at (default: Prod Read Replica) and never
    writes to Applications - it only creates a session-scoped #temp table to stage the
    CSV for a single set-based JOIN, then reports what it found.

        For each source CSV row, produces one of:
            - Unique   : exactly one matching Application found and that Application is matched by one source row.
            - Ambiguous: 2+ matching Applications found, or another source row also matched the same Application.
            - NoMatch  : 0 matching Applications found.

    Match key: ParentFirstName + ParentLastName + ParentDateOfBirth + cleaned NINO +
    ChildFirstName + ChildLastName + ChildDateOfBirth + School URN, restricted to
    Applications.LocalAuthorityID = -LocalAuthorityId, Applications.Type = -ApplicationType,
    and the Created UTC window. Establishment is only carried through as a manual
    sanity-check column (source vs matched school).

        IMPORTANT (data quality, confirmed against the Herts nursery corrected-email file):
            - "Parent DOB" and "Child DOB" are ISO format yyyy-MM-dd.
            - Rows with blank Parent NI Number will never match (SQL NULL <> NULL) and will
                correctly fall out as NoMatch for manual review.

    Nothing containing real NINOs/emails is printed to the console - only row counts and
    classifications. The detailed report (written to -ReportOutputPath) DOES contain PII
    (NINO is not included, but names/DOB/email are) - handle it with the same care as the
    source CSV.

.PARAMETER CsvPath
    Path to the source CSV (columns: Reference, Status, Parent First Name, Parent Last Name,
    Parent Email, Parent DOB, Parent NI Number, Child First Name, Child Last Name, Child DOB,
    Establishment, School URN).

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

.PARAMETER LocalAuthorityId
    Applications.LocalAuthorityID to restrict matching to. Defaults to 919 (Hertfordshire).

.PARAMETER CreatedFromUtc
    Inclusive Applications.Created UTC lower bound. Defaults to 2026-09-23T00:00:00.

.PARAMETER CreatedBeforeUtc
    Exclusive Applications.Created UTC upper bound. Defaults to 2026-09-24T00:00:00.

.EXAMPLE
    .\Invoke-ParentEmailMatch.ps1 -CsvPath "C:\Users\ATATTI\OneDrive - Department for Education\Bulk import applications\Herts\nursery\Revised application with correct email addresses 23 09 2026.csv"

.EXAMPLE
    .\Invoke-ParentEmailMatch.ps1 -CsvPath .\data\herts-corrected-parent-emails.csv `
        -Server "ece-database.database.windows.net" -Database "read-replica" `
        -CreatedFromUtc "2026-09-23T00:00:00" -CreatedBeforeUtc "2026-09-24T00:00:00"

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

    [string]$ApplicationType = "FreeSchoolMeals",

    [int]$LocalAuthorityId = 919,

    [datetime]$CreatedFromUtc = [datetime]"2026-09-23T00:00:00",

    [datetime]$CreatedBeforeUtc = [datetime]"2026-09-24T00:00:00"
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

$requiredHeaders = @(
    "Reference",
    "Status",
    "Parent First Name",
    "Parent Last Name",
    "Parent Email",
    "Parent DOB",
    "Parent NI Number",
    "Child First Name",
    "Child Last Name",
    "Child DOB",
    "Establishment",
    "School URN"
)
$actualHeaders = if ($sourceRows.Count -gt 0) { $sourceRows[0].PSObject.Properties.Name } else { @() }
$missingHeaders = @($requiredHeaders | Where-Object { $actualHeaders -notcontains $_ })
if ($missingHeaders.Count -gt 0) {
    throw "CSV is missing required header(s): $($missingHeaders -join ', ')"
}

# ---------------------------------------------------------------------------
# Build a typed DataTable matching the #Staging schema. Rows that fail to
# parse (bad/missing DOB) are excluded from staging and counted separately -
# they are never silently dropped without being reported.
# ---------------------------------------------------------------------------
$table = New-Object System.Data.DataTable
[void]$table.Columns.Add("RowNum", [int])
[void]$table.Columns.Add("SourceReference", [string])
[void]$table.Columns.Add("SourceStatus", [string])
[void]$table.Columns.Add("ParentFirstName", [string])
[void]$table.Columns.Add("ParentLastName", [string])
[void]$table.Columns.Add("ParentDOB", [datetime])
[void]$table.Columns.Add("ParentNinoClean", [string])
[void]$table.Columns.Add("ChildFirstName", [string])
[void]$table.Columns.Add("ChildLastName", [string])
[void]$table.Columns.Add("ChildDOB", [datetime])
[void]$table.Columns.Add("SchoolUrn", [int])
[void]$table.Columns.Add("SchoolName", [string])
[void]$table.Columns.Add("NewParentEmail", [string])

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$parseErrors = New-Object System.Collections.Generic.List[string]
$rowNum = 0

foreach ($row in $sourceRows) {
    $rowNum++
    try {
        $parentDob = [datetime]::ParseExact($row.'Parent DOB'.Trim(), 'yyyy-MM-dd', $culture)
        $childDob = [datetime]::ParseExact($row.'Child DOB'.Trim(), 'yyyy-MM-dd', $culture)
        $schoolUrn = [int]::Parse($row.'School URN'.Trim(), $culture)
    }
    catch {
        $parseErrors.Add("Row $rowNum : DOB or School URN parse failed")
        continue
    }

    $ninoClean = ($row.'Parent NI Number' -replace '\s', '').Trim().ToUpperInvariant()

    $dr = $table.NewRow()
    $dr.RowNum = $rowNum
    $dr.SourceReference = $row.Reference.Trim()
    $dr.SourceStatus = $row.Status.Trim()
    $dr.ParentFirstName = $row.'Parent First Name'.Trim()
    $dr.ParentLastName = $row.'Parent Last Name'.Trim()
    $dr.ParentDOB = $parentDob
    $dr.ParentNinoClean = $ninoClean
    $dr.ChildFirstName = $row.'Child First Name'.Trim()
    $dr.ChildLastName = $row.'Child Last Name'.Trim()
    $dr.ChildDOB = $childDob
    $dr.SchoolUrn = $schoolUrn
    $dr.SchoolName = $row.Establishment.Trim()
    $dr.NewParentEmail = $row.'Parent Email'.Trim()
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
    SourceReference VARCHAR(8),
    SourceStatus VARCHAR(100),
    ParentFirstName VARCHAR(100),
    ParentLastName VARCHAR(100),
    ParentDOB DATE,
    ParentNinoClean VARCHAR(50),
    ChildFirstName VARCHAR(50),
    ChildLastName VARCHAR(50),
    ChildDOB DATE,
    SchoolUrn INT,
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

    Write-Host "  Staged $($table.Rows.Count) rows. Running match query (LA=$LocalAuthorityId, Type='$ApplicationType', Created >= $($CreatedFromUtc.ToString('u')), Created < $($CreatedBeforeUtc.ToString('u'))) ..." -ForegroundColor Cyan

    $matchCmd = $conn.CreateCommand()
    $matchCmd.CommandTimeout = 120
    $matchCmd.CommandText = @"
SELECT
    s.RowNum,
    s.SourceReference, s.SourceStatus,
    s.ParentFirstName, s.ParentLastName, s.ChildFirstName, s.ChildLastName,
    s.NewParentEmail AS ProposedNewEmail,
    a.ApplicationID, a.Reference AS MatchedReference, a.ParentEmail AS CurrentParentEmail,
    s.SchoolUrn AS SourceSchoolUrn, s.SchoolName AS SourceSchool, e.EstablishmentName AS MatchedSchool,
    COUNT(a.ApplicationID) OVER (PARTITION BY s.RowNum) AS MatchCountForRow,
    CASE
        WHEN a.ApplicationID IS NULL THEN 0
        ELSE COUNT(s.RowNum) OVER (PARTITION BY a.ApplicationID)
    END AS SourceRowsMatchedToApplication
FROM #Staging s
LEFT JOIN Applications a
    ON a.Type = @ApplicationType
    AND a.LocalAuthorityID = @LocalAuthorityId
    AND a.Created >= @CreatedFromUtc
    AND a.Created < @CreatedBeforeUtc
    AND a.EstablishmentId = s.SchoolUrn
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
    [void]$matchCmd.Parameters.AddWithValue("@LocalAuthorityId", $LocalAuthorityId)
    [void]$matchCmd.Parameters.AddWithValue("@CreatedFromUtc", $CreatedFromUtc)
    [void]$matchCmd.Parameters.AddWithValue("@CreatedBeforeUtc", $CreatedBeforeUtc)

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
[void]$reportTable.Columns.Add("ProposedEmailMissing", [bool])
[void]$reportTable.Columns.Add("UpdateCandidate", [bool])
foreach ($r in $reportTable.Rows) {
    $count = [int]$r["MatchCountForRow"]
    $sourceRowsMatchedToApplication = [int]$r["SourceRowsMatchedToApplication"]
    $r["Classification"] = if ($count -eq 1 -and $sourceRowsMatchedToApplication -eq 1) { "Unique" } elseif ($count -eq 0) { "NoMatch" } else { "Ambiguous" }
    $r["SchoolMismatch"] = -not ([string]::Equals([string]$r["SourceSchool"], [string]$r["MatchedSchool"], [System.StringComparison]::OrdinalIgnoreCase))
    $r["ProposedEmailMissing"] = [string]::IsNullOrWhiteSpace([string]$r["ProposedNewEmail"])
    $r["UpdateCandidate"] = $r["Classification"] -eq "Unique" -and $r["ProposedEmailMissing"] -eq $false
}

$reportTable | Export-Csv -Path $ReportOutputPath -NoTypeInformation

$uniqueRows = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "Unique" })
$ambiguousRowNums = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "Ambiguous" } | Select-Object -ExpandProperty RowNum -Unique)
$noMatchRowNums = @($reportTable.Rows | Where-Object { $_["Classification"] -eq "NoMatch" } | Select-Object -ExpandProperty RowNum -Unique)
$schoolMismatchCount = @($uniqueRows | Where-Object { $_["SchoolMismatch"] -eq $true }).Count
$missingProposedEmailCount = @($reportTable.Rows | Where-Object { $_["ProposedEmailMissing"] -eq $true } | Select-Object -ExpandProperty RowNum -Unique).Count
$updateCandidateCount = @($reportTable.Rows | Where-Object { $_["UpdateCandidate"] -eq $true }).Count

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Green
Write-Host "Source rows read:        $($sourceRows.Count)"
Write-Host "Parse errors (excluded): $($parseErrors.Count)"
Write-Host "Unique matches:          $($uniqueRows.Count)"
Write-Host "  of which school name mismatch (sanity-check flag): $schoolMismatchCount"
Write-Host "  of which update candidates with non-blank proposed email: $updateCandidateCount"
Write-Host "Ambiguous (2+ matches):  $($ambiguousRowNums.Count)"
Write-Host "No match (0 matches):    $($noMatchRowNums.Count)"
Write-Host "Rows with blank proposed email: $missingProposedEmailCount"
Write-Host "Report written to:       $ReportOutputPath"
Write-Host ""
Write-Host "Report contains PII (names/DOB/email) - handle with the same care as the source CSV." -ForegroundColor Yellow
