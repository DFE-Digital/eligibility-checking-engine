<#
.SYNOPSIS
    Turns the Hertfordshire-approved email files (tab A "proposed" and tab B
    "resolved") into a single SQL script that loads dbo.zz_HertsEmailBackfill_Stage.

.DESCRIPTION
    Companion to Apply-HertfordshireParentEmailBackfill.sql. Reads the two
    workbooks that were sent to / approved by Hertfordshire, keeps only the
    columns the apply script needs, and writes INSERT statements (1,000 rows
    per statement) to a .sql file next to the input files.

    The output .sql contains ~18k email addresses - keep it in the OneDrive
    Herts folder with the source workbooks, NOT in the repo.

    Input is .xlsx (needs the ImportExcel module: Install-Module ImportExcel
    -Scope CurrentUser) or .csv (no dependencies - File > Save As > CSV UTF-8
    from Excel). Column names must match the Analyse script output:
      A : ApplicationReference, ProposedEmail, EmailType, MatchBasis
      B : ApplicationReference, ChosenEmail,   EmailType, RuleApplied

.EXAMPLE
    # Tab A only (first run)
    .\Convert-HertsEmailStaging.ps1 -ProposedFile "C:\...\Herts\Herts-email-backfill-A-proposed.xlsx"

.EXAMPLE
    # Tab B resolved only (later, once Hertfordshire confirm the rule)
    .\Convert-HertsEmailStaging.ps1 -ResolvedFile "C:\...\Herts\Herts-email-backfill-B-resolved.xlsx"

.EXAMPLE
    # Both in one go
    .\Convert-HertsEmailStaging.ps1 `
        -ProposedFile  "C:\...\Herts\Herts-email-backfill-A-proposed.xlsx" `
        -ResolvedFile  "C:\...\Herts\Herts-email-backfill-B-resolved.xlsx"
#>
[CmdletBinding()]
param(
    [string] $ProposedFile,          # tab A workbook (optional if -ResolvedFile given)
    [string] $ResolvedFile,          # tab B-resolved workbook (optional if -ProposedFile given)
    [string] $OutputFile,            # default: Stage-HertsEmail-<A|B|AB>.sql next to the first input
    [int]    $RowsPerInsert = 1000
)

if (-not $ProposedFile -and -not $ResolvedFile) { throw 'Give -ProposedFile, -ResolvedFile, or both.' }
if (-not $OutputFile) {
    $first  = if ($ProposedFile) { $ProposedFile } else { $ResolvedFile }
    $suffix = ($(if ($ProposedFile) { 'A' }) + $(if ($ResolvedFile) { 'B' }))
    $OutputFile = Join-Path (Split-Path $first) "Stage-HertsEmail-$suffix.sql"
}

$ErrorActionPreference = 'Stop'

function Read-Sheet([string] $Path) {
    switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.csv'  { return Import-Csv -LiteralPath $Path -Encoding UTF8 }
        '.xlsx' {
            if (-not (Get-Module -ListAvailable ImportExcel)) {
                throw "ImportExcel module not found. Either 'Install-Module ImportExcel -Scope CurrentUser' or save '$Path' as CSV UTF-8 and pass the .csv."
            }
            return Import-Excel -Path $Path
        }
        default { throw "Unsupported file type: $Path" }
    }
}

function Normalize-Reference([string] $Value) {
    $v = ($Value ?? '').Trim()
    # Excel may have dropped leading zeros if the cell became numeric
    if ($v -match '^\d{1,8}$') { return $v.PadLeft(8, '0') }
    return $v
}

function Sql($Value) {
    if ($null -eq $Value -or $Value -eq '') { return 'NULL' }
    "'" + ([string]$Value -replace "'", "''") + "'"
}

$EmailPattern = '^[^@\s]+@[^@\s]+\.[^@\s]+$'

# Last year's spreadsheets occasionally had a note typed after the address,
# e.g. "head@school.org Use for HERTSFX (not Admin address)" or a stray "? ?".
# Take the first whitespace-delimited token, trim trailing punctuation, and
# only accept it if the result is a clean single address AND the raw value
# contained exactly one '@' (so "a@x.com and b@y.org" is still rejected).
# Returns the clean email and the original value (recorded in staging).
function Clean-Email([string] $Raw) {
    $v = ($Raw ?? '').Trim()
    if ($v -match $EmailPattern) { return @{ Email = $v; Original = $null } }
    if (($v.ToCharArray() | Where-Object { $_ -eq '@' }).Count -ne 1) { return @{ Email = $v; Original = $null } }
    $tok = ($v -split '\s+')[0].TrimEnd('?', '.', ',', ';', ':', ')')
    if ($tok -match $EmailPattern) { return @{ Email = $tok; Original = $v } }
    return @{ Email = $v; Original = $null }   # leave as-is; will fail validation below
}

$rows = New-Object System.Collections.Generic.List[object]
$padded = 0

function Add-Row([string] $RefRaw, [string] $EmailRaw, [string] $Source, [string] $EmailType, [string] $Basis) {
    $ref = Normalize-Reference $RefRaw
    if ($ref -ne ($RefRaw ?? '').Trim()) { $script:padded++ }
    $e = Clean-Email $EmailRaw
    $rows.Add([pscustomobject]@{
        Reference = $ref; Email = $e.Email; OriginalEmail = $e.Original
        Source = $Source; EmailType = $EmailType; Basis = $Basis })
}

if ($ProposedFile) {
    foreach ($r in (Read-Sheet $ProposedFile)) {
        Add-Row $r.ApplicationReference $r.ProposedEmail 'A' $r.EmailType $r.MatchBasis
    }
}
if ($ResolvedFile) {
    foreach ($r in (Read-Sheet $ResolvedFile)) {
        Add-Row $r.ApplicationReference $r.ChosenEmail 'B' $r.EmailType $r.RuleApplied
    }
}

# --- sanity checks before writing anything -----------------------------------
$bad = $rows | Where-Object { $_.Reference -notmatch '^\d{8}$' -or $_.Email -notmatch $EmailPattern }
if ($bad) {
    $bad | Select-Object -First 10 | Format-Table | Out-String | Write-Warning
    throw "$($bad.Count) row(s) have an invalid reference or email - fix the source file first."
}
$dups = $rows | Group-Object Reference | Where-Object Count -gt 1
if ($dups) { throw "$($dups.Count) duplicate reference(s) in the input, e.g. $($dups[0].Name)." }

$cleaned = @($rows | Where-Object { $_.OriginalEmail })
if ($cleaned) {
    Write-Host "Cleaned $($cleaned.Count) email(s) that had a note/punctuation appended:"
    $cleaned | Group-Object OriginalEmail | ForEach-Object {
        Write-Host ("  {0,3} x  '{1}'  ->  '{2}'" -f $_.Count, $_.Name, $_.Group[0].Email)
    }
}

# --- emit SQL ----------------------------------------------------------------
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine(@"
-- Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm') by Convert-HertsEmailStaging.ps1
--   A (proposed): $(if ($ProposedFile) { Split-Path -Leaf $ProposedFile } else { '(not included in this run)' })
--   B (resolved): $(if ($ResolvedFile) { Split-Path -Leaf $ResolvedFile } else { '(not included in this run)' })
-- Run against the PRIMARY database before Apply-HertfordshireParentEmailBackfill.sql.
-- Safe to run more than once with different files (e.g. A now, B later): the
-- table is created if missing and rows are appended; the primary key on
-- Reference rejects any reference that is already staged.
SET NOCOUNT ON;
IF OBJECT_ID('dbo.zz_HertsEmailBackfill_Stage') IS NULL
CREATE TABLE dbo.zz_HertsEmailBackfill_Stage (
    Reference     VARCHAR(8)    NOT NULL PRIMARY KEY,
    ParentEmail   VARCHAR(1000) NOT NULL,
    Source        CHAR(1)       NOT NULL,   -- A = tab A proposed, B = tab B resolved
    EmailType     VARCHAR(50)   NULL,
    Basis         VARCHAR(100)  NULL,
    OriginalEmail VARCHAR(1000) NULL        -- set when the approved value had a note appended and was cleaned
);
GO
"@)

for ($i = 0; $i -lt $rows.Count; $i += $RowsPerInsert) {
    $chunk = $rows[$i..([Math]::Min($i + $RowsPerInsert, $rows.Count) - 1)]
    [void]$sb.AppendLine('INSERT INTO dbo.zz_HertsEmailBackfill_Stage (Reference, ParentEmail, Source, EmailType, Basis, OriginalEmail) VALUES')
    [void]$sb.AppendLine((($chunk | ForEach-Object {
        "($(Sql $_.Reference), $(Sql $_.Email), $(Sql $_.Source), $(Sql $_.EmailType), $(Sql $_.Basis), $(Sql $_.OriginalEmail))"
    }) -join ",`r`n") + ';')
    [void]$sb.AppendLine('GO')
}

[void]$sb.AppendLine("SELECT Source, COUNT(*) AS Rows FROM dbo.zz_HertsEmailBackfill_Stage GROUP BY Source;")
[IO.File]::WriteAllText($OutputFile, $sb.ToString(), [Text.UTF8Encoding]::new($false))

Write-Host "Rows: $($rows.Count)  (A=$(($rows | Where-Object Source -eq 'A').Count), B=$(($rows | Where-Object Source -eq 'B').Count))"
if ($padded) { Write-Warning "$padded reference(s) were left-padded with zeros to 8 digits - the apply script's preview will confirm they all resolve." }
Write-Host "Written: $OutputFile"
