<#
.SYNOPSIS
Combines signed PEM certificate bundles and their private keys into PFX files.

.PARAMETER CertificatePath
Parent directory containing the environment folders. Defaults to the script directory.

.PARAMETER Environment
Environments to process. Defaults to all four.

.PARAMETER OpenSSLPath
Path to openssl.exe. If omitted, OpenSSL is located on PATH or in common installation paths.

.EXAMPLE
.\Generate-SignedClientAuthPfx.ps1

.EXAMPLE
.\Generate-SignedClientAuthPfx.ps1 -Environment DEV,TEST

#>

[CmdletBinding()]
param(
    [string]$CertificatePath = $PSScriptRoot,
    [ValidateSet('DEV', 'TEST', 'PP', 'PROD')]
    [string[]]$Environment = @('DEV', 'TEST', 'PP', 'PROD'),
    [string]$OpenSSLPath
)

$ErrorActionPreference = 'Stop'

if (-not $OpenSSLPath) {
    $opensslCommand = Get-Command openssl -ErrorAction SilentlyContinue
    if ($opensslCommand) {
        $OpenSSLPath = $opensslCommand.Source
    }
    else {
        $commonPaths = @(
            'C:\Program Files\Git\usr\bin\openssl.exe',
            'C:\Program Files (x86)\Git\usr\bin\openssl.exe',
            'C:\OpenSSL-Win64\bin\openssl.exe',
            'C:\OpenSSL\bin\openssl.exe'
        )
        $OpenSSLPath = $commonPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}

if (-not $OpenSSLPath) {
    throw 'OpenSSL not found. Install OpenSSL or specify -OpenSSLPath.'
}

function Invoke-OpenSslWithPassword {
    param([string[]]$Arguments, [string]$Password)

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $OpenSSLPath
    $startInfo.Arguments = ($Arguments | ForEach-Object {
            '"' + (($_ -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
        }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($Password + "`n")
        $process.StandardInput.Close()
        $process.WaitForExit()
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output   = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

$domains = @{
    DEV  = 'www.dev.eligibility-checking-engine.education.gov.uk'
    TEST = 'www.test.eligibility-checking-engine.education.gov.uk'
    PP   = 'www.pp.eligibility-checking-engine.education.gov.uk'
    PROD = 'www.eligibility-checking-engine.education.gov.uk'
}

$jobs = foreach ($envName in ($Environment | Select-Object -Unique)) {
    $domain = $domains[$envName]
    $folder = Join-Path $CertificatePath $envName
    $key = Join-Path $folder "$domain.key"
    $pem = Join-Path $folder (($domain -replace '\.', '_') + '.pem')
    $pfx = Join-Path $folder "$domain.pfx"
    $base64Path = "$pfx.base64"

    foreach ($inputPath in @($key, $pem)) {
        if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
            throw "Required input file not found: $inputPath"
        }
    }
    if (Test-Path -LiteralPath $pfx) {
        throw "Output already exists; refusing to overwrite: $pfx"
    }
    if (Test-Path -LiteralPath $base64Path) {
        throw "Output already exists; refusing to overwrite: $base64Path"
    }

    $certPublicKey = @(& $OpenSSLPath x509 -in $pem -pubkey -noout 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read the first certificate for ${envName}: $($certPublicKey -join [Environment]::NewLine)"
    }
    $keyPublicKey = @(& $OpenSSLPath pkey -in $key -pubout 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read the private key for ${envName}: $($keyPublicKey -join [Environment]::NewLine)"
    }
    # Compare the public key of the first certificate in the PEM bundle with the public key of the private key to ensure they match.
    # The first certificate in the PEM bundle should always be the one corresponding to the private key.
    if (($certPublicKey -join "`n") -cne ($keyPublicKey -join "`n")) {
        throw "The first certificate in the $envName PEM bundle does not match its private key."
    }

    $identityCheck = @(& $OpenSSLPath x509 -in $pem -noout -checkhost $domain 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "The first certificate in the $envName PEM bundle does not identify $domain`: $($identityCheck -join [Environment]::NewLine)"
    }

    $validityOutput = @(& $OpenSSLPath x509 -in $pem -dateopt iso_8601 -noout -startdate -enddate 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read the validity period for the first $envName certificate: $($validityOutput -join [Environment]::NewLine)"
    }
    $validityText = $validityOutput -join "`n"
    $notBeforeMatch = [regex]::Match($validityText, '(?m)^notBefore=(.+)$')
    $notAfterMatch = [regex]::Match($validityText, '(?m)^notAfter=(.+)$')
    if (-not $notBeforeMatch.Success -or -not $notAfterMatch.Success) {
        throw "Could not determine the validity period for the first $envName certificate."
    }
    try {
        $notBefore = [DateTime]::Parse($notBeforeMatch.Groups[1].Value.Trim(), [Globalization.CultureInfo]::InvariantCulture)
        $notAfter = [DateTime]::Parse($notAfterMatch.Groups[1].Value.Trim(), [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "Could not parse the validity period for the first $envName certificate: $($_.Exception.Message)"
    }
    $now = [DateTime]::UtcNow
    if ($now -lt $notBefore.ToUniversalTime() -or $now -ge $notAfter.ToUniversalTime()) {
        throw "The first certificate in the $envName PEM bundle is not currently valid (valid from $notBefore through $notAfter)."
    }

    $certificateDetails = @(& $OpenSSLPath x509 -in $pem -noout -text 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot inspect extensions for the first $envName certificate: $($certificateDetails -join [Environment]::NewLine)"
    }
    $ekuMatch = [regex]::Match(($certificateDetails -join "`n"), '(?is)X509v3 Extended Key Usage:\s*(?<eku>.*?)(?=\r?\n\s*X509v3 [^:\r\n]+:|\r?\n\s*Signature Algorithm:|\z)')
    if (-not $ekuMatch.Success -or $ekuMatch.Groups['eku'].Value -notmatch '(?i)TLS Web Client Authentication|clientAuth|1\.3\.6\.1\.5\.5\.7\.3\.2') {
        throw "The first certificate in the $envName PEM bundle does not include the Client Authentication EKU."
    }

    [pscustomobject]@{ Environment = $envName; Domain = $domain; Key = $key; Pem = $pem; Pfx = $pfx; Base64 = $base64Path }
}

foreach ($job in $jobs) {
    Write-Host "Creating PFX for $($job.Environment) ($($job.Domain))"
    $temporaryPfx = Join-Path (Split-Path -Parent $job.Pfx) ('.pfx-' + [guid]::NewGuid().ToString() + '.tmp')
    $temporaryBase64 = Join-Path (Split-Path -Parent $job.Pfx) ('.pfx-' + [guid]::NewGuid().ToString() + '.tmp')
    try {
        Write-Warning 'The PFX and base64 files contain a private key protected only by an empty password. Store both securely.'
        $password = ''
        $result = Invoke-OpenSslWithPassword -Password $password -Arguments @('pkcs12', '-export', '-in', $job.Pem, '-inkey', $job.Key, '-name', $job.Domain, '-out', $temporaryPfx, '-passout', 'stdin')
        if ($result.ExitCode -ne 0) {
            throw "PFX export failed for $($job.Environment): $($result.Output)"
        }
        $result = Invoke-OpenSslWithPassword -Password $password -Arguments @('pkcs12', '-in', $temporaryPfx, '-noout', '-passin', 'stdin')
        if ($result.ExitCode -ne 0) {
            throw "PFX verification failed for $($job.Environment): $($result.Output)"
        }
        $pfxBytes = [IO.File]::ReadAllBytes($temporaryPfx)
        try {
            $base64Value = [Convert]::ToBase64String($pfxBytes)
            $stream = [IO.File]::Open($temporaryBase64, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try {
                $writer = [IO.StreamWriter]::new($stream, [Text.ASCIIEncoding]::new())
                try { $writer.Write($base64Value) } finally { $writer.Dispose() }
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            [Array]::Clear($pfxBytes, 0, $pfxBytes.Length)
            $base64Value = $null
        }
        Move-Item -LiteralPath $temporaryPfx -Destination $job.Pfx -ErrorAction Stop
        Write-Host "Created: $($job.Pfx)"
        Move-Item -LiteralPath $temporaryBase64 -Destination $job.Base64 -ErrorAction Stop
        Write-Host "Created: $($job.Base64)"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPfx) {
            Remove-Item -LiteralPath $temporaryPfx -Force
        }
        if (Test-Path -LiteralPath $temporaryBase64) {
            Remove-Item -LiteralPath $temporaryBase64 -Force
        }
    }
}
