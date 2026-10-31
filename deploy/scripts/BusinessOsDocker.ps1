# Shared helpers for the BusinessOS local Docker scripts.
# PowerShell 5.1 compatible. Dot-source this file; do not execute it directly.

$ErrorActionPreference = 'Stop'

function Get-BusinessOsRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-BusinessOsEnvironments {
    return @{
        dev = @{
            Name = 'dev'
            Project = 'businessos-dev'
            EnvFile = 'deploy\env\.env.dev'
            Example = 'deploy\env\env.dev.example'
            Override = 'compose.dev.yaml'
            FrontendUrl = 'http://127.0.0.1:4200'
            ApiUrl = 'http://127.0.0.1:5162'
            Containers = @('businessos-dev-api-1', 'businessos-dev-web-1', 'businessos-dev-qdrant-1', 'businessos-dev-sqlserver-1')
        }
        staging = @{
            Name = 'staging'
            Project = 'businessos-staging'
            EnvFile = 'deploy\env\.env.staging'
            Example = 'deploy\env\env.staging.example'
            Override = 'compose.staging.yaml'
            FrontendUrl = 'https://127.0.0.1:4201'
            ApiUrl = 'http://127.0.0.1:5102'
            Containers = @('businessos-staging-api-1', 'businessos-staging-web-1', 'businessos-staging-qdrant-1', 'businessos-staging-sqlserver-1')
        }
        'prod-local' = @{
            Name = 'prod-local'
            Project = 'businessos-prodlocal'
            EnvFile = 'deploy\env\.env.prod-local'
            Example = 'deploy\env\env.prod-local.example'
            Override = 'compose.prod-local.yaml'
            FrontendUrl = 'https://127.0.0.1:4202'
            ApiUrl = 'http://127.0.0.1:5103'
            Containers = @('businessos-prodlocal-api-1', 'businessos-prodlocal-web-1', 'businessos-prodlocal-qdrant-1', 'businessos-prodlocal-sqlserver-1')
        }
    }
}

function Get-BlockedDatabaseNames {
    return @(
        'BusinessOS',
        'ExamDynamics',
        'nopcommerce',
        'ProdFalconDb',
        'SupportFlowDb',
        'master',
        'model',
        'msdb',
        'tempdb'
    )
}

function Get-AllowedDockerDatabases {
    return @('BusinessOS_DockerDev', 'BusinessOS_Staging', 'BusinessOS_ProdLocal')
}

function Read-BusinessOsEnvFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing environment file: $Path. Run deploy\scripts\setup-local.ps1 first."
    }

    $map = @{}
    foreach ($line in Get-Content -Path $Path) {
        if ($line -match '^\s*#' -or $line -match '^\s*$') {
            continue
        }

        $parts = $line -split '=', 2
        if ($parts.Count -ne 2) {
            continue
        }

        $key = $parts[0].Trim().TrimStart([char]0xFEFF)
        $map[$key] = $parts[1].Trim()
    }

    return $map
}

function Assert-BusinessOsDatabaseName {
    param(
        [Parameter(Mandatory = $true)][string]$DatabaseName,
        [switch]$AllowCustomDatabase
    )

    $blocked = Get-BlockedDatabaseNames
    foreach ($name in $blocked) {
        if ($DatabaseName -eq $name) {
            throw "Refusing to use database '$DatabaseName'. Docker environments must use BusinessOS_DockerDev, BusinessOS_Staging, or BusinessOS_ProdLocal."
        }
    }

    $allowed = Get-AllowedDockerDatabases
    $known = $false
    foreach ($name in $allowed) {
        if ($DatabaseName -eq $name) {
            $known = $true
        }
    }

    if (-not $known -and -not $AllowCustomDatabase) {
        throw "Database '$DatabaseName' is not one of the local Docker databases. Pass -AllowCustomDatabase only if you intend to use another empty database."
    }
}

function Assert-DockerDaemon {
    & docker version --format '{{.Server.Version}}' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Desktop is not running. Start Docker Desktop, wait until it is ready, then run this command again."
    }
}

function New-BusinessOsToken {
    param([Parameter(Mandatory = $true)][int]$ByteCount)

    $buffer = New-Object byte[] $ByteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($buffer)
    }
    finally {
        $rng.Dispose()
    }

    return ([System.BitConverter]::ToString($buffer)).Replace('-', '').ToLowerInvariant()
}

function Initialize-BusinessOsEnvFile {
    param(
        [Parameter(Mandatory = $true)][string]$ExampleRelativePath,
        [Parameter(Mandatory = $true)][string]$TargetRelativePath
    )

    $root = Get-BusinessOsRoot
    $example = Join-Path $root $ExampleRelativePath
    $target = Join-Path $root $TargetRelativePath
    if (Test-Path $target) {
        Write-Host "$TargetRelativePath already exists and was left unchanged."
        return
    }

    $jwt = New-BusinessOsToken -ByteCount 48
    $sql = 'Aa1!' + (New-BusinessOsToken -ByteCount 16)
    $text = [System.IO.File]::ReadAllText($example)
    $text = $text.TrimStart([char]0xFEFF).Replace('CHANGE_ME_JWT', $jwt).Replace('CHANGE_ME_SQL', $sql)
    $directory = Split-Path $target -Parent
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($target, $text, $utf8)
    Write-Host "Created $TargetRelativePath. Secrets were generated and were not printed."
}

function New-BusinessOsLocalCertificate {
    $root = Get-BusinessOsRoot
    $certDir = Join-Path $root 'deploy\certs'
    $crt = Join-Path $certDir 'localhost.crt'
    $key = Join-Path $certDir 'localhost.key'
    $der = Join-Path $certDir 'localhost.der'
    if ((Test-Path $crt) -and (Test-Path $key)) {
        Write-Host 'Local TLS certificate already exists and was left unchanged.'
        return
    }

    if (-not (Test-Path $certDir)) {
        New-Item -ItemType Directory -Path $certDir | Out-Null
    }

    $work = Join-Path $env:TEMP ('businessos-cert-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
'@
        $program = @'
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

var crtPath = args[0];
var keyPath = args[1];
var derPath = args[2];
using var rsa = RSA.Create(2048);
var request = new CertificateRequest(new X500DistinguishedName("CN=localhost"), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder();
san.AddDnsName("localhost");
san.AddIpAddress(IPAddress.Loopback);
request.CertificateExtensions.Add(san.Build());
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
File.WriteAllText(crtPath, cert.ExportCertificatePem());
File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
File.WriteAllBytes(derPath, cert.Export(X509ContentType.Cert));
'@
        [System.IO.File]::WriteAllText((Join-Path $work 'cert.csproj'), $csproj)
        [System.IO.File]::WriteAllText((Join-Path $work 'Program.cs'), $program)
        & dotnet run --project (Join-Path $work 'cert.csproj') -c Release -- $crt $key $der
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not create the local TLS certificate.'
        }
    }
    finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }

    $trust = Start-Job -ScriptBlock {
        param($Path)
        Import-Certificate -FilePath $Path -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
    } -ArgumentList $der
    if (Wait-Job $trust -Timeout 20) {
        Receive-Job $trust -ErrorAction SilentlyContinue | Out-Null
        if ($trust.State -eq 'Completed') {
            Write-Host 'Created the localhost certificate and trusted it for the current Windows user.'
        }
        else {
            Write-Host 'Created deploy\certs\localhost.crt. Import deploy\certs\localhost.der into Current User > Trusted Root Certification Authorities.'
        }
    }
    else {
        Stop-Job $trust
        Write-Host 'Created deploy\certs\localhost.crt. Windows did not finish the trust prompt. Import deploy\certs\localhost.der into Current User > Trusted Root Certification Authorities.'
    }

    Remove-Job $trust -Force
}

function Get-RedactedText {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    return [regex]::Replace($Text, '(?i)(Password\s*=\s*)[^;''\s]+', '$1***')
}

function Wait-BusinessOsContainer {
    param(
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $status = & docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $ContainerName 2>$null
        if ($status -eq 'healthy') {
            Write-Host "$ContainerName is healthy."
            return
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    throw "$ContainerName did not become healthy within $TimeoutSeconds seconds. Run: docker logs $ContainerName"
}

function Get-BusinessOsComposeArguments {
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentName,
        [hashtable]$EnvMap
    )

    $environments = Get-BusinessOsEnvironments
    $environment = $environments[$EnvironmentName]
    if ($null -eq $environment) {
        throw "Unknown environment '$EnvironmentName'."
    }

    $root = Get-BusinessOsRoot
    $fileArgs = @('-f', (Join-Path $root 'compose.yaml'), '-f', (Join-Path $root $environment.Override))
    if ($null -ne $EnvMap -and $EnvMap['SQL_MODE'] -eq 'container') {
        $fileArgs += @('-f', (Join-Path $root 'compose.sql.yaml'))
    }

    $envFile = Join-Path $root $environment.EnvFile
    return @{
        Project = $environment.Project
        FileArgs = $fileArgs
        EnvFile = $envFile
        Definition = $environment
    }
}

function Invoke-BusinessOsCompose {
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentName,
        [Parameter(Mandatory = $true)][string[]]$ComposeArgs,
        [hashtable]$EnvMap
    )

    $compose = Get-BusinessOsComposeArguments -EnvironmentName $EnvironmentName -EnvMap $EnvMap
    $argumentList = @('compose', '-p', $compose.Project) + $compose.FileArgs + @('--env-file', $compose.EnvFile) + $ComposeArgs
    & docker @argumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed for $EnvironmentName (exit $LASTEXITCODE)."
    }
}

function Get-MigrationConnectionString {
    param([Parameter(Mandatory = $true)][hashtable]$EnvMap)

    $server = $EnvMap['SQL_SERVER']
    if ($EnvMap['SQL_MODE'] -eq 'container') {
        $server = '127.0.0.1,' + $EnvMap['SQL_HOST_PORT']
    }
    elseif ($server -like 'host.docker.internal*') {
        $server = $server.Replace('host.docker.internal', 'localhost')
    }

    return 'Server=' + $server +
        ';Database=' + $EnvMap['SQL_DATABASE'] +
        ';User Id=' + $EnvMap['SQL_USER'] +
        ';Password=' + $EnvMap['SQL_PASSWORD'] +
        ';Encrypt=True;TrustServerCertificate=True;Connect Timeout=60'
}

function Get-HostExpressConnectionString {
    return 'Server=localhost\SQLEXPRESS;Database=BusinessOS;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=60'
}
