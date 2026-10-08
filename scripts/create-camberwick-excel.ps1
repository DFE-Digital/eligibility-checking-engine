# =============================================================================
# create-camberwick-excel.ps1
# Creates an Excel workbook documenting the Camberwick FSM expansion test
# organisations. Uses the ImportExcel module if available, otherwise falls
# back to Excel COM automation.
#
# Columns per DfE register format:
#   Role | LegalName | Type | Address | URN | UID | LA Code |
#   UKPRN | UPIN | Establishment Number | Local Authority | Legacy ID | FSM Access
#
#   URN              – schools and academies only
#   UID              – MAT only
#   LA Code          – local authority only
#   Establishment No – schools and academies only
#   Local Authority  – schools and academies only
# =============================================================================

$OutputPath = Join-Path $PSScriptRoot "camberwick-fsm-organisations.xlsx"

# All numeric fields stored as strings to avoid COM Int32 cast errors.
# The sheet will still display them as plain numbers.
$data = @(
    [PSCustomObject]@{
        Role                = "An LA - FSM basic version"
        LegalName           = "CAMBERWICK COUNCIL"
        Type                = "Local Authority"
        Address             = "Town Hall, 1 The Green, Camberwick, CW1 1AA"
        URN                 = ""
        UID                 = ""
        LACode              = "9000"
        UKPRN               = "10099000"
        UPIN                = "999000"
        EstablishmentNumber = ""
        LocalAuthority      = ""
        LegacyID            = "9000"
        FsmAccess           = "Yes"
    },
    [PSCustomObject]@{
        Role                = "A MAT - FSM enhanced version"
        LegalName           = "CAMBERWICK ACADEMY TRUST"
        Type                = "Multi-academy trust"
        Address             = "1 The Green, Camberwick, CW1 1AA"
        URN                 = ""
        UID                 = "9001"
        LACode              = ""
        UKPRN               = "10099001"
        UPIN                = "999001"
        EstablishmentNumber = ""
        LocalAuthority      = ""
        LegacyID            = "9001"
        FsmAccess           = "Yes"
    },
    [PSCustomObject]@{
        Role                = "A school - FSM enhanced version"
        LegalName           = "CAMBERWICK COMMUNITY SCHOOL"
        Type                = "Community School"
        Address             = "1 School Lane, Camberwick, CW1 2BB"
        URN                 = "900001"
        UID                 = ""
        LACode              = ""
        UKPRN               = "10099002"
        UPIN                = "999002"
        EstablishmentNumber = "9001"
        LocalAuthority      = "Camberwick Council (9000)"
        LegacyID            = "9000001"
        FsmAccess           = "Yes"
    },
    [PSCustomObject]@{
        Role                = "An academy - FSM enhanced version"
        LegalName           = "CAMBERWICK ACADEMY"
        Type                = "Academy Converter"
        Address             = "2 Academy Road, Camberwick, CW1 3CC"
        URN                 = "900002"
        UID                 = ""
        LACode              = ""
        UKPRN               = "10099003"
        UPIN                = "999003"
        EstablishmentNumber = "9002"
        LocalAuthority      = "Camberwick Council (9000)"
        LegacyID            = "9000002"
        FsmAccess           = "Yes"
    }
)

$headers = @("Role", "LegalName", "Type", "Address", "URN", "UID", "LA Code",
             "UKPRN", "UPIN", "Establishment Number", "Local Authority", "Legacy ID", "FSM Access")

# ── Try ImportExcel (no Excel installation required) ─────────────────────────
if (Get-Module -ListAvailable -Name ImportExcel) {
    Import-Module ImportExcel

    $data | Select-Object `
        Role, LegalName, Type, Address, URN, UID,
        @{N="LA Code";E={$_.LACode}},
        UKPRN, UPIN,
        @{N="Establishment Number";E={$_.EstablishmentNumber}},
        @{N="Local Authority";E={$_.LocalAuthority}},
        @{N="Legacy ID";E={$_.LegacyID}},
        @{N="FSM Access";E={$_.FsmAccess}} |
    Export-Excel -Path $OutputPath `
        -WorksheetName "Camberwick Organisations" `
        -TableName "CamberwickOrgs" `
        -TableStyle Medium6 `
        -AutoSize `
        -FreezeTopRow `
        -BoldTopRow

    Write-Host "Excel file created (ImportExcel): $OutputPath"
}
# ── Fallback: Excel COM automation (requires Microsoft Excel installed) ───────
elseif (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe") {
    Write-Host "ImportExcel module not found. Falling back to Excel COM automation..."

    $excel         = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $workbook      = $excel.Workbooks.Add()
    $sheet         = $workbook.Worksheets.Item(1)
    $sheet.Name    = "Camberwick Organisations"

    # Headers
    for ($col = 0; $col -lt $headers.Count; $col++) {
        $cell                   = $sheet.Cells.Item(1, $col + 1)
        $cell.Value2            = $headers[$col]
        $cell.Font.Bold         = $true
        $cell.Interior.ColorIndex = 37   # blue
        $cell.Font.ColorIndex   = 2      # white
    }

    # Data rows — all values are already strings, so no cast issues
    $props = @("Role","LegalName","Type","Address","URN","UID","LACode",
               "UKPRN","UPIN","EstablishmentNumber","LocalAuthority","LegacyID","FsmAccess")

    for ($row = 0; $row -lt $data.Count; $row++) {
        $item = $data[$row]
        for ($col = 0; $col -lt $props.Count; $col++) {
            $sheet.Cells.Item($row + 2, $col + 1).Value2 = $item.($props[$col])
        }
    }

    # Auto-fit columns
    $sheet.UsedRange.EntireColumn.AutoFit() | Out-Null

    $workbook.SaveAs($OutputPath)
    $workbook.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null

    Write-Host "Excel file created (COM): $OutputPath"
}
else {
    Write-Error "Neither the ImportExcel module nor Microsoft Excel is available. Install ImportExcel with: Install-Module ImportExcel -Scope CurrentUser"
    exit 1
}
