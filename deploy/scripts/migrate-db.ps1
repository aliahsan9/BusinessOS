# Explicit EF Core migration commands.
# The default action is list, which does not change a database.
# update applies migrations to the selected environment only.
# This script never drops or recreates a database.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'staging', 'prod-local', 'host')]
    [string]$Environment,

    [ValidateSet('list', 'update', 'script')]
    [string]$Action = 'list',

    [switch]$ConfirmExistingDevelopmentDatabase,
    [switch]$AllowCustomDatabase
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

$root = Get-BusinessOsRoot
Set-Location $root
$project = Join-Path $root 'BusinessOS.Infrastructure\BusinessOS.Infrastructure.csproj'
$startup = Join-Path $root 'BusinessOS.API\BusinessOS.API.csproj'

if ($Environment -eq 'host') {
    Write-Host 'Target: Windows SQL Server Express instance SQLEXPRESS, database BusinessOS.'
    Write-Host 'This is the existing development database used by dotnet run.'
    if ($Action -ne 'list' -and -not $ConfirmExistingDevelopmentDatabase) {
        throw 'Refusing to change the existing BusinessOS database. Re-run with -ConfirmExistingDevelopmentDatabase if that is intentional.'
    }

    $connection = Get-HostExpressConnectionString
}
else {
    $definition = (Get-BusinessOsEnvironments)[$Environment]
    $envMap = Read-BusinessOsEnvFile -Path (Join-Path $root $definition.EnvFile)
    Assert-BusinessOsDatabaseName -DatabaseName $envMap['SQL_DATABASE'] -AllowCustomDatabase:$AllowCustomDatabase
    Write-Host "Target environment: $Environment"
    Write-Host "Target database: $($envMap['SQL_DATABASE'])"
    if ($envMap['SQL_MODE'] -eq 'container') {
        Write-Host "SQL Server container loopback port: $($envMap['SQL_HOST_PORT'])"
    }
    else {
        Write-Host 'SQL mode: host. The Windows sqlcmd/dotnet process uses localhost, not host.docker.internal.'
    }

    $connection = Get-MigrationConnectionString -EnvMap $envMap
}

if ($Action -eq 'list') {
    $output = & dotnet ef migrations list --project $project --startup-project $startup --configuration Release --connection $connection 2>&1
    $code = $LASTEXITCODE
    Write-Host (Get-RedactedText -Text ($output | Out-String))
    exit $code
}

if ($Action -eq 'script') {
    $outputDir = Join-Path $root 'deploy\migrations'
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }

    $output = Join-Path $outputDir ($Environment + '.sql')
    $outputText = & dotnet ef migrations script --idempotent --project $project --startup-project $startup --configuration Release --connection $connection --output $output 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        Write-Host (Get-RedactedText -Text ($outputText | Out-String))
        exit $code
    }

    Write-Host "Wrote an idempotent SQL script to $output. Review it before applying it."
    exit 0
}

$output = & dotnet ef database update --project $project --startup-project $startup --configuration Release --connection $connection 2>&1
$code = $LASTEXITCODE
Write-Host (Get-RedactedText -Text ($output | Out-String))
exit $code
