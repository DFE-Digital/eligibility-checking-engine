<#
.SYNOPSIS
    Calls the DWP CAPI POST /v2/citizens/match endpoint directly, using the same request shape
    as CheckingEngineGateway.DwpCitizenCheck / DwpAdapter.GetCitizen, to obtain a real citizen
    GUID for later use with Invoke-DwpCitizenLookup.ps1.

.DESCRIPTION
    Pulls Dwp:* config/secrets from a Key Vault (same "Dwp--" naming as Invoke-DwpCitizenLookup.ps1),
    builds the mTLS client certificate, gets an OAuth2 token, then POSTs a match request built
    exactly like CitizenMatchRequest in CheckYourEligibility.API.Boundary.Requests.DWP:

        { "jsonapi": { "version": "1.0" },
          "data": { "type": "Match",
                    "attributes": { "dateOfBirth": ..., "ninoFragment": ..., "lastName": ... } } }

    ninoFragment is computed the same way the app does it - NOT simply the last 4 characters:
        nino.Substring(nino.Length - 5, 4)

    On a 200, DWP returns a citizen GUID you can plug into Invoke-DwpCitizenLookup.ps1 (doc
    section 8.1) or Invoke-DwpCitizenClaims.ps1. On 404, no citizen matched the details supplied.

    Note: our onboarded matching policy uses lastName + dateOfBirth + NINO fragment - NOT the
    firstName/lastName/dateOfBirth/postCode "simplest match" shown as a general example in DWP's
    docs. That example doesn't apply to how this app (policy-id "ece") is configured to match.

.PARAMETER KeyVaultName
    Name of the Azure Key Vault to read Dwp secrets from (e.g. the Test environment's vault).

.PARAMETER LastName
    Last name to match against. Use your own details for Test - never use real personal data
    (yours or anyone else's) against Prod for ad-hoc testing.

.PARAMETER DateOfBirth
    Date of birth in yyyy-MM-dd format.

.PARAMETER Nino
    Full National Insurance Number. Only the computed 4-digit fragment is sent to DWP, and the
    full value is never written to disk or logged by this script - only held in memory.

.PARAMETER Context
    DWP "context" header value. Defaults to "DFE-FSM" (Free School Meals) - matches GetContext()
    in DwpAdapter.cs. Use "DFE-EYPP" or "DFE-2EY" if you specifically need those check types.

.PARAMETER SecretPrefix
    Secret name prefix in Key Vault. Defaults to "Dwp--".

.EXAMPLE
    ./Invoke-DwpCitizenMatch.ps1 -KeyVaultName ece-test-kv-ece -LastName Smith -DateOfBirth 1990-05-14 -Nino AB123456C
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$KeyVaultName,

    [Parameter(Mandatory)]
    [string]$LastName,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string]$DateOfBirth,

    [Parameter(Mandatory)]
    [securestring]$Nino,

    [string]$Context = "DFE-FSM",

    [string]$SecretPrefix = "Dwp--",

    [switch]$SkipServerCertificateCheck
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "Run this with PowerShell 7+ (pwsh), not Windows PowerShell 5.1."
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

function Get-DwpSecret {
    param([Parameter(Mandatory)][string]$Name, [switch]$Optional)
    $full = "$SecretPrefix$Name"
    try {
        return Get-AzKeyVaultSecret -VaultName $KeyVaultName -Name $full -AsPlainText -ErrorAction Stop
    }
    catch {
        if ($Optional) { return $null }
        Write-Error "Could not read secret '$full' from vault '$KeyVaultName' (run Invoke-DwpCitizenLookup.ps1 -ListSecrets to check names). $_"
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
$instigatingUserId = Get-DwpSecret "ApiInstigatingUserId" -Optional
$policyId          = Get-DwpSecret "ApiPolicyId" -Optional

if (-not $apiHost) { $apiHost = "" }

# Guard rail - refuse to silently hit anything that looks like production
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

$restParams = @{}
if ($SkipServerCertificateCheck) {
    Write-Warning "Skipping DWP server certificate validation - only do this if you understand why."
    $restParams["SkipCertificateCheck"] = $true
}

# --- Get an OAuth2 token (mTLS required) ---
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

# --- Compute ninoFragment exactly like CheckingEngineGateway.DwpCitizenCheck does ---
# Nino is a SecureString so it's masked at the interactive prompt and never echoed;
# only decrypted to plaintext in memory here, just long enough to slice the fragment.
$ninoPlain = [System.Net.NetworkCredential]::new([string]::Empty, $Nino).Password
if ($ninoPlain.Length -lt 5) {
    Write-Error "NINO too short to compute a 4-digit fragment (need at least 5 characters)."
    exit 1
}
$ninoFragment = $ninoPlain.Substring($ninoPlain.Length - 5, 4)
$ninoPlain = $null
Write-Host "Computed ninoFragment: $ninoFragment (from NINO, not logged in full)" -ForegroundColor DarkGray

# --- Build the match request body, matching CitizenMatchRequest.cs exactly ---
$matchBody = @{
    jsonapi = @{ version = "1.0" }
    data    = @{
        type       = "Match"
        attributes = @{
            dateOfBirth  = $DateOfBirth
            ninoFragment = $ninoFragment
            lastName     = $LastName
        }
    }
} | ConvertTo-Json -Depth 5

$uri = "$baseUrl$apiHost/v2/citizens/match"
$correlationId = [Guid]::NewGuid().ToString()

$headers = @{
    Authorization         = "Bearer $accessToken"
    "context"             = $Context
    "correlation-id"      = $correlationId
    "instigating-user-id" = $instigatingUserId
    "policy-id"           = $policyId
}

Write-Host ""
Write-Host "POST $uri" -ForegroundColor Cyan
Write-Host "correlation-id: $correlationId" -ForegroundColor DarkGray

if ($PSCmdlet.ShouldProcess($uri, "POST citizen match request to DWP CAPI")) {
    try {
        $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers `
            -Body $matchBody -ContentType "application/json" -Certificate $cert @restParams
        $guid = $response.data.id
        Write-Host "MATCH FOUND" -ForegroundColor Green
        Write-Host "Citizen GUID: $guid" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next step:" -ForegroundColor Cyan
        Write-Host "  pwsh -File .\Invoke-DwpCitizenLookup.ps1 -KeyVaultName $KeyVaultName -CitizenGuid $guid"
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 404) {
            Write-Host "NO MATCH (404) - no citizen found for the details supplied." -ForegroundColor Yellow
        }
        else {
            Write-Host "FAILED - HTTP $status" -ForegroundColor Red
        }
        Write-Host ($_.ErrorDetails.Message ?? $_.Exception.Message) -ForegroundColor Red
    }
}
