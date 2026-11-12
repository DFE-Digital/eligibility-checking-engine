<#
.SYNOPSIS
Generates Certificate Signing Requests (CSRs) with Client Authentication for multiple environments.

.DESCRIPTION
Creates private keys and CSRs for the following domains:
- www.dev.eligibility-checking-engine.education.gov.uk
- www.test.eligibility-checking-engine.education.gov.uk
- www.pp.eligibility-checking-engine.education.gov.uk
- www.eligibility-checking-engine.education.gov.uk

All CSRs are configured with ONLY the clientAuth Extended Key Usage.

.PARAMETER OutputPath
The directory where CSR and key files will be created. Defaults to current script directory.

.PARAMETER OpenSSLPath
Path to openssl.exe. Defaults to Git's OpenSSL if available.

.EXAMPLE
.\Generate-ClientAuthCSRs.ps1
.\Generate-ClientAuthCSRs.ps1 -OutputPath "C:\certs"
#>

param(
    [string]$OutputPath = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [string]$OpenSSLPath
)

# Define domains and their configurations
$domains = @(
    @{
        Domain = "www.dev.eligibility-checking-engine.education.gov.uk"
        Environment = "DEV"
    },
    @{
        Domain = "www.test.eligibility-checking-engine.education.gov.uk"
        Environment = "TEST"
    },
    @{
        Domain = "www.pp.eligibility-checking-engine.education.gov.uk"
        Environment = "PP"
    },
    @{
        Domain = "www.eligibility-checking-engine.education.gov.uk"
        Environment = "PROD"
    }
)

# Verify output path exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Find OpenSSL
if (-not $OpenSSLPath) {
    $opensslCmd = Get-Command openssl -ErrorAction SilentlyContinue
    if ($opensslCmd) {
        $OpenSSLPath = $opensslCmd.Source
    } else {
        # Check common paths
        $commonPaths = @(
            "C:\Program Files\Git\usr\bin\openssl.exe",
            "C:\Program Files (x86)\Git\usr\bin\openssl.exe",
            "C:\OpenSSL-Win64\bin\openssl.exe",
            "C:\OpenSSL\bin\openssl.exe"
        )
        
        foreach ($path in $commonPaths) {
            if (Test-Path $path) {
                $OpenSSLPath = $path
                break
            }
        }
    }
}

if (-not $OpenSSLPath) {
    Write-Error "OpenSSL not found. Please ensure Git is installed or provide the -OpenSSLPath parameter."
    exit 1
}

# Create CSR config template
function New-CSRConfig {
    param(
        [string]$Domain,
        [string]$Environment
    )
    
    $config = @"
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = req_distinguished_name
req_extensions = v3_req

[req_distinguished_name]
C = GB
S = Greater Manchester
L = Manchester
O = Department for Education
CN = $Domain

[v3_req]
extendedKeyUsage = clientAuth
"@
    
    return $config
}

# Process each domain
$results = @()
$processedCount = 0

foreach ($domainConfig in $domains) {
    $domain = $domainConfig.Domain
    $environment = $domainConfig.Environment
    
    Write-Host "Processing: $domain ($environment)"
    
    # Create environment-specific folder
    $envFolder = Join-Path $OutputPath $environment
    if (-not (Test-Path $envFolder)) {
        New-Item -ItemType Directory -Path $envFolder -Force | Out-Null
    }
    
    $keyPath = Join-Path $envFolder "$domain.key"
    $csrPath = Join-Path $envFolder "$domain.csr"
    $configPath = Join-Path $envFolder "$domain.conf"
    
    try {
        # Write config file
        $config = New-CSRConfig -Domain $domain -Environment $environment
        Set-Content -Path $configPath -Value $config -Encoding ASCII
        
        # Generate private key
        Write-Host " - Generating 2048-bit RSA private key..."
        & $OpenSSLPath genrsa -out $keyPath 2048 2>&1 | Out-Null
        
        if (-not (Test-Path $keyPath)) {
            throw "Failed to generate private key"
        }
        
        # Generate CSR
        Write-Host " - Creating Certificate Signing Request..."
        $subj = "/C=GB/ST=Greater Manchester/L=Manchester/O=Department for Education/CN=$domain"
        & $OpenSSLPath req -new -key $keyPath -out $csrPath -config $configPath -subj $subj 2>&1 | Out-Null
        
        if (-not (Test-Path $csrPath)) {
            throw "Failed to generate CSR"
        }
        
        # Verify CSR
        Write-Host " - Verifying CSR..."
        $csrInfo = & $OpenSSLPath req -in $csrPath -noout -text 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  -- Success!"
            $results += @{
                Domain = $domain
                Environment = $environment
                Status = "Success"
                KeyPath = $keyPath
                CSRPath = $csrPath
                Error = $null
            }
            $processedCount++
        } else {
            throw "CSR verification failed"
        }
        
    } catch {
        Write-Host "  -- Error: $_"
        $results += @{
            Domain = $domain
            Environment = $environment
            Status = "Failed"
            KeyPath = $keyPath
            CSRPath = $csrPath
            Error = $_
        }
    }
    
    Write-Host ""
}

exit 0
