<#
.SYNOPSIS
    Calls the DWP CAPI GET /v2/citizens/{guid} endpoint (doc section 8.1) directly, using the
    same OAuth2 + mutual-TLS credentials the app itself uses, pulled live from Azure Key Vault.

.DESCRIPTION
    Mirrors what CheckYourEligibility.API.Adapters.DwpAdapter does (see DwpAdapter.cs):
      1. Reads Dwp:* config (BaseUrl, ApiHost, ApiTokenUrl, ApiCertificate, ApiClientId,
         ApiSecret, ApiContext, ApiAccessLevel, ApiInstigatingUserId, ApiPolicyId) from a
         Key Vault.
      2. Builds an X509Certificate2 from the base64 PFX secret for mutual TLS.
      3. POSTs to the token endpoint (client_credentials grant) using that cert.
      4. Sends a single GET to /v2/citizens/{guid} with the bearer token + standard CAPI headers.

    Deliberately GET-only. This endpoint also supports PATCH per the DWP doc, but PATCH can
    write real citizen data at DWP - not something to try ad-hoc, especially against Prod.

    IMPORTANT - secret naming assumption: this assumes Key Vault secret names match the
    AddAzureKeyVault convention of replacing ':' with '--', e.g. config key "Dwp:ApiCertificate"
    -> secret name "Dwp--ApiCertificate" (see AddAzureKeyVault in Program.cs). If your vault
    uses different naming, run this script with -ListSecrets first to see what's actually in
    there, then override with -SecretPrefix or the individual -*SecretName parameters.

.PARAMETER KeyVaultName
    Name of the Azure Key Vault to read Dwp secrets from (e.g. the Test environment's vault).

.PARAMETER CitizenGuid
    The DWP citizen GUID to look up (e.g. one seen in a prior successful citizen-match response
    in App Insights or the CAPIAudit table - see the dwp-check-error-lookup skill).

.PARAMETER SecretPrefix
    Secret name prefix in Key Vault. Defaults to "Dwp--". Change if your vault's naming differs.

.PARAMETER ListSecrets
    Just lists secret names in the vault that look Dwp-related, then exits. Use this first if
    you're not sure of the naming convention.

.PARAMETER SkipServerCertificateCheck
    Skips validation of DWP's server certificate. Do NOT use this against a real DWP host unless
    you have a specific, understood reason (e.g. a known cert chain issue in Test) - it disables
    a security control. Never use against Prod.

.EXAMPLE
    ./Invoke-DwpCitizenLookup.ps1 -KeyVaultName kv-cye-test -ListSecrets

.EXAMPLE
    ./Invoke-DwpCitizenLookup.ps1 -KeyVaultName kv-cye-test -CitizenGuid 07eb97f04b2996188c3e61ac7424b2af4afb853e2e9fa791d0efa59e763b78be
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$KeyVaultName,

    [Parameter(ParameterSetName = 'Lookup', Mandatory)]
    [string]$CitizenGuid,

    [Parameter(ParameterSetName = 'Lookup')]
    [string]$SecretPrefix = "Dwp--",

    [Parameter(ParameterSetName = 'List', Mandatory)]
    [switch]$ListSecrets,

    [Parameter(ParameterSetName = 'Lookup')]
    [switch]$SkipServerCertificateCheck
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "Run this with PowerShell 7+ (pwsh), not Windows PowerShell 5.1 - '-Certificate' on Invoke-RestMethod and other bits here need PS7."
    exit 1
}

if (-not (Get-Module -ListAvailable -Name Az.KeyVault)) {
    Write-Error "Az.KeyVault module not found. Install with: Install-Module Az.KeyVault -Scope CurrentUser"
    exit 1
}

if (-not (Get-AzContext)) {
    Write-Host "Not logged into Azure - launching login..." -ForegroundColor Yellow
    Connect-AzAccount | Out-Null
}

# --- Discovery mode: just show what's in the vault ---
if ($ListSecrets) {
    Write-Host "Secrets in vault '$KeyVaultName' matching '*dwp*':" -ForegroundColor Cyan
    Get-AzKeyVaultSecret -VaultName $KeyVaultName |
        Where-Object { $_.Name -like "*dwp*" } |
        Select-Object Name, Enabled, Updated |
        Format-Table -AutoSize
    exit 0
}

function Get-DwpSecret {
    param([Parameter(Mandatory)][string]$Name, [switch]$Optional)
    $full = "$SecretPrefix$Name"
    try {
        return Get-AzKeyVaultSecret -VaultName $KeyVaultName -Name $full -AsPlainText -ErrorAction Stop
    }
    catch {
        if ($Optional) { return $null }
        Write-Error "Could not read secret '$full' from vault '$KeyVaultName' (run with -ListSecrets to see actual names). $_"
        throw
    }
}

Write-Host "Reading Dwp:* config from Key Vault '$KeyVaultName' (prefix '$SecretPrefix')..." -ForegroundColor Cyan
$baseUrl           = Get-DwpSecret "BaseUrl"
$apiHost           = Get-DwpSecret "ApiHost" -Optional
$tokenUrl          = Get-DwpSecret "ApiTokenUrl"
$certB64           = Get-DwpSecret "ApiCertificate"
$clientId          = Get-DwpSecret "ApiClientId"
$clientSecret      = Get-DwpSecret "ApiSecret"
$context           = Get-DwpSecret "ApiContext" -Optional
$accessLevel       = Get-DwpSecret "ApiAccessLevel" -Optional
$instigatingUserId = Get-DwpSecret "ApiInstigatingUserId" -Optional
$policyId          = Get-DwpSecret "ApiPolicyId" -Optional

if (-not $context) { $context = "abc-1-ab-x12888" }
if (-not $accessLevel) { $accessLevel = "1" }
if (-not $apiHost) { $apiHost = "" }

# Guard rail - make sure a prod-looking host gets a deliberate confirmation
if ($baseUrl -match "prd|production") {
    Write-Warning "This host looks like PRODUCTION: $baseUrl"
    $confirm = Read-Host "Type PROD to continue, anything else aborts"
    if ($confirm -ne "PROD") {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 1
    }
}

# --- Build the mTLS client certificate from the base64 PFX secret ---
Write-Host "Building client certificate..." -ForegroundColor Cyan
$certBytes = [Convert]::FromBase64String($certB64)
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certBytes,
    [string]$null,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet
)
Write-Host "Cert subject: $($cert.Subject), expires: $($cert.NotAfter)" -ForegroundColor DarkGray

$restParams = @{}
if ($SkipServerCertificateCheck) {
    Write-Warning "Skipping DWP server certificate validation - only do this if you understand why."
    $restParams["SkipCertificateCheck"] = $true
}

# --- Get an OAuth2 token (mTLS required on this call too, per DWP docs) ---
Write-Host "Requesting OAuth2 token from $tokenUrl..." -ForegroundColor Cyan
$tokenBody = @{
    client_id     = $clientId
    client_secret = $clientSecret
    grant_type    = "client_credentials"
}
try {
    $tokenResponse = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $tokenBody `
        -ContentType "application/x-www-form-urlencoded" -Certificate $cert @restParams
}
catch {
    Write-Host "Token request FAILED:" -ForegroundColor Red
    Write-Host ($_.ErrorDetails.Message ?? $_.Exception.Message) -ForegroundColor Red
    throw
}
$accessToken = $tokenResponse.access_token
Write-Host "Got token, expires in $($tokenResponse.expires_in)s" -ForegroundColor Green

# --- GET /v2/citizens/{guid} (doc section 8.1) ---
$uri = "$baseUrl$apiHost/v2/citizens/$CitizenGuid"
$correlationId = [Guid]::NewGuid().ToString()

$headers = @{
    Authorization         = "Bearer $accessToken"
    "context"             = $context
    "access-level"        = $accessLevel
    "correlation-id"      = $correlationId
    "instigating-user-id" = $instigatingUserId
    "policy-id"           = $policyId
}

Write-Host ""
Write-Host "GET $uri" -ForegroundColor Cyan
Write-Host "correlation-id: $correlationId  (use this to find the call in DWP's logs if needed)" -ForegroundColor DarkGray

if ($PSCmdlet.ShouldProcess($uri, "GET citizen record from DWP CAPI")) {
    try {
        $response = Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -Certificate $cert @restParams
        Write-Host "SUCCESS" -ForegroundColor Green
        $response | ConvertTo-Json -Depth 10
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "FAILED - HTTP $status" -ForegroundColor Red
        Write-Host ($_.ErrorDetails.Message ?? $_.Exception.Message) -ForegroundColor Red
    }
}
