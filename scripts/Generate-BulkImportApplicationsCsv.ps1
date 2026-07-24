<#
.SYNOPSIS
    Generates a CSV of randomised test applications for the
    /application/bulk-import endpoint.

.DESCRIPTION
    Produces a CSV matching the header/column format expected by
    ApplicationBulkImportRowMap (CheckYourEligibility.API):

        Parent First Name, Parent Surname, Parent DOB, Parent Nino,
        Parent Email Address, Child First Name, Child Surname,
        Child Date of Birth, Child School URN, Eligibility End Date,
        Application Status, tier

    All dates are yyyy-MM-dd. NINOs are generated using only valid
    (non-prohibited) prefixes. 'Application Status' and 'tier' are left
    blank by default (defaults to Receiving on import) unless
    -ApplicationStatus / -IncludeRandomStatuses is used.

.PARAMETER Count
    Number of application rows to generate. Defaults to 10.

.PARAMETER EstablishmentUrn
    The Child School URN to use for every row. Defaults to 9002
    (Camberwick Community School placeholder URN - this is what
    currently exists in DEV as of 2026-07-10; once the Camberwick
    establishment URN migration - see
    scripts/Backfill-CamberwickEstablishmentUrns.sql - has been run
    against an environment, use 600001 (Camberwick Community School) or
    600002 (Camberwick Academy) instead).

.PARAMETER OutputPath
    Full path for the generated CSV. Defaults to
    "$PWD\bulk-import-applications-test.csv".

.PARAMETER ApplicationStatus
    Optional. If supplied, every row gets this exact Application Status
    value (must be one of: Entitled, Receiving, EvidenceNeeded,
    SentForReview, ReviewedEntitled, ReviewedNotEntitled, Archived).
    Ignored if -IncludeRandomStatuses is used.

.PARAMETER IncludeRandomStatuses
    Optional switch. When set, each row is given a random status from a
    realistic mix (mostly blank/Receiving, with some Entitled /
    EvidenceNeeded / SentForReview) instead of leaving the column blank.
    Useful for exercising the ELIG-2617 status-history fix with varied
    statuses. Ignored if -ApplicationStatus is supplied.

.PARAMETER Seed
    Optional. Random seed for reproducible output.

.EXAMPLE
    # 10 applications for Camberwick Community School (DEV placeholder URN)
    .\Generate-BulkImportApplicationsCsv.ps1

.EXAMPLE
    # 25 applications for a specific school, with varied statuses
    .\Generate-BulkImportApplicationsCsv.ps1 -EstablishmentUrn 9003 -Count 25 -IncludeRandomStatuses

.EXAMPLE
    # Positional shorthand: URN then count
    .\Generate-BulkImportApplicationsCsv.ps1 600001 25

.EXAMPLE
    # Post-URN-migration Dev/Test, explicit output path
    .\Generate-BulkImportApplicationsCsv.ps1 -EstablishmentUrn 600001 -OutputPath "C:\Test Data\camberwick-bulk-import.csv"
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [int]$EstablishmentUrn = 9002,

    [Parameter(Position = 1)]
    [int]$Count = 10,

    [string]$OutputPath = (Join-Path $PWD 'bulk-import-applications-test.csv'),

    [ValidateSet('Entitled', 'Receiving', 'EvidenceNeeded', 'SentForReview', 'ReviewedEntitled', 'ReviewedNotEntitled', 'Archived')]
    [string]$ApplicationStatus,

    [switch]$IncludeRandomStatuses,

    [int]$Seed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSBoundParameters.ContainsKey('Seed')) {
    $random = [System.Random]::new($Seed)
}
else {
    $random = [System.Random]::new()
}

# Valid NI prefixes (prohibited pairs BG GB KN NK NT TN ZZ excluded)
$NiPrefixes = @(
    'AA', 'AB', 'AE', 'AH', 'AJ', 'AK', 'AL', 'AM', 'AP', 'AR', 'AS', 'AT', 'AW', 'AX', 'AY', 'AZ',
    'BA', 'BB', 'BE', 'BH', 'BJ', 'BK', 'BL', 'BM', 'BP', 'BR', 'BS', 'BT', 'BW', 'BX', 'BY', 'BZ',
    'CA', 'CB', 'CE', 'CH', 'CJ', 'CK', 'CL', 'CM', 'CP', 'CR', 'CS', 'CT', 'CW', 'CX', 'CY', 'CZ',
    'EA', 'EB', 'EE', 'EH', 'EJ', 'EK', 'EL', 'EM', 'EP', 'ER', 'ES', 'ET', 'EW', 'EX', 'EY', 'EZ',
    'HA', 'HB', 'HE', 'HH', 'HJ', 'HK', 'HL', 'HM', 'HP', 'HR', 'HS', 'HT', 'HW', 'HX', 'HY', 'HZ',
    'JA', 'JB', 'JC', 'JE', 'JH', 'JJ', 'JK', 'JL', 'JM', 'JP', 'JR', 'JS', 'JT', 'JW', 'JX', 'JY',
    'KA', 'KB', 'KE', 'KH', 'KJ', 'KK', 'KL', 'KM', 'KP', 'KR', 'KS', 'KT', 'KW', 'KX', 'KY',
    'LA', 'LB', 'LE', 'LH', 'LJ', 'LK', 'LL', 'LM', 'LP', 'LR', 'LS', 'LT', 'LW', 'LX', 'LY', 'LZ',
    'MA', 'MB', 'ME', 'MH', 'MJ', 'MK', 'ML', 'MM', 'MP', 'MR', 'MS', 'MT', 'MW', 'MX', 'MY', 'MZ',
    'NA', 'NB', 'NE', 'NH', 'NJ', 'NL', 'NM', 'NP', 'NR', 'NS', 'NW', 'NX', 'NY', 'NZ',
    'PA', 'PB', 'PC', 'PE', 'PH', 'PJ', 'PK', 'PL', 'PM', 'PP', 'PR', 'PS', 'PT', 'PW', 'PX', 'PY',
    'RA', 'RB', 'RE', 'RH', 'RJ', 'RK', 'RL', 'RM', 'RP', 'RR', 'RS', 'RT', 'RW', 'RX', 'RY', 'RZ',
    'SA', 'SB', 'SC', 'SE', 'SH', 'SJ', 'SK', 'SL', 'SM', 'SP', 'SR', 'SS', 'ST', 'SW', 'SX', 'SY', 'SZ',
    'TA', 'TB', 'TE', 'TH', 'TJ', 'TK', 'TL', 'TM', 'TP', 'TR', 'TS', 'TT', 'TW', 'TX', 'TY',
    'WA', 'WB', 'WE', 'WH', 'WJ', 'WK', 'WL', 'WM', 'WP', 'WR', 'WS', 'WT', 'WW', 'WX', 'WY',
    'XA', 'XB', 'XE', 'XH', 'XJ', 'XK', 'XL', 'XM', 'XP', 'XR', 'XS', 'XT', 'XW', 'XX', 'XY',
    'YA', 'YB', 'YE', 'YH', 'YJ', 'YK', 'YL', 'YM', 'YP', 'YR', 'YS', 'YT', 'YW', 'YX', 'YY'
)
$NiSuffixes = @('A', 'B', 'C', 'D')

$FirstNames = @(
    'James', 'Oliver', 'Harry', 'Jack', 'George', 'Noah', 'Charlie', 'Jacob', 'Alfie', 'Freddie',
    'Olivia', 'Amelia', 'Isla', 'Ava', 'Mia', 'Isabella', 'Sophie', 'Poppy', 'Emily', 'Lily',
    'Mohammed', 'Aisha', 'Fatima', 'Omar', 'Layla', 'Yusuf', 'Zara', 'Ibrahim', 'Sara', 'Ali'
)
$LastNames = @(
    'Smith', 'Jones', 'Williams', 'Taylor', 'Brown', 'Davies', 'Evans', 'Wilson', 'Thomas', 'Roberts',
    'Johnson', 'Lewis', 'Walker', 'Robinson', 'Wood', 'Thompson', 'White', 'Watson', 'Jackson', 'Wright',
    'Green', 'Harris', 'Cooper', 'King', 'Lee', 'Martin', 'Clarke', 'Morgan', 'Hughes',
    'Allen', 'Anderson', 'Bailey', 'Baker', 'Bell', 'Bennett', 'Carter', 'Clark', 'Collins', 'Cook'
)
# Realistic mix used when -IncludeRandomStatuses is set (weighted towards blank/Receiving)
$RandomStatusPool = @('', '', '', '', 'Entitled', 'Entitled', 'EvidenceNeeded', 'SentForReview')

function Get-RandomItem {
    param([array]$Items)
    return $Items[$random.Next(0, $Items.Count)]
}

function New-Nino {
    $prefix = Get-RandomItem -Items $NiPrefixes
    $digits = $random.Next(0, 999999).ToString('D6')
    $suffix = Get-RandomItem -Items $NiSuffixes
    return "$prefix$digits$suffix"
}

function New-Dob {
    param([int]$MinAge, [int]$MaxAge)
    $today = Get-Date
    $age = $random.Next($MinAge, $MaxAge + 1)
    $daysOffset = $random.Next(0, 365)
    return $today.AddYears(-$age).AddDays(-$daysOffset).ToString('yyyy-MM-dd')
}

$rows = New-Object System.Collections.Generic.List[string]
$rows.Add('Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date,Application Status,tier')

for ($i = 1; $i -le $Count; $i++) {
    $parentFirstName = Get-RandomItem -Items $FirstNames
    $surname = Get-RandomItem -Items $LastNames
    $childFirstName = Get-RandomItem -Items $FirstNames
    $parentDob = New-Dob -MinAge 22 -MaxAge 55
    $childDob = New-Dob -MinAge 4 -MaxAge 15
    $nino = New-Nino
    $email = "$($parentFirstName.ToLower()).$($surname.ToLower())$i@example.com"
    $eligibilityEndDate = (Get-Date).AddYears(1).ToString('yyyy-MM-dd')

    $status = ''
    if ($ApplicationStatus) {
        $status = $ApplicationStatus
    }
    elseif ($IncludeRandomStatuses) {
        $status = Get-RandomItem -Items $RandomStatusPool
    }

    $rows.Add("$parentFirstName,$surname,$parentDob,$nino,$email,$childFirstName,$surname,$childDob,$EstablishmentUrn,$eligibilityEndDate,$status,")
}

$rows | Out-File -FilePath $OutputPath -Encoding utf8

Write-Host "Generated $Count application rows -> $OutputPath"
Write-Host "Establishment URN used: $EstablishmentUrn"
if ($EstablishmentUrn -in 9002, 9003) {
    Write-Host "NOTE: 9002/9003 are the OLD Camberwick placeholder URNs. Confirm these still exist in the target environment (Dev still uses them as of 2026-07-10); once an environment has run scripts/Backfill-CamberwickEstablishmentUrns.sql, use 600001/600002 instead." -ForegroundColor Yellow
}
