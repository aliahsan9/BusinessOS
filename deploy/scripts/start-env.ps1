# Starts one Docker environment. Does not apply EF Core migrations.
# When the database has pending migrations, dependency containers stay up and the API is not started.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'staging', 'prod-local')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

Assert-DockerDaemon
$root = Get-BusinessOsRoot
Set-Location $root

$definition = (Get-BusinessOsEnvironments)[$Environment]
$envPath = Join-Path $root $definition.EnvFile
$envMap = Read-BusinessOsEnvFile -Path $envPath
Assert-BusinessOsDatabaseName -DatabaseName $envMap['SQL_DATABASE']

if ($envMap['SQL_MODE'] -eq 'container') {
    $freeGb = [math]::Round((Get-PSDrive -Name C).Free / 1GB, 1)
    if ($freeGb -lt 8) {
        throw "C: has $freeGb GB free. Refusing to pull the optional SQL Server image because it needs several gigabytes for the image and database files. Free disk space or switch this environment to SQL_MODE=host."
    }

    Write-Host "Starting SQL Server and Qdrant for $Environment. The database volume is kept across restarts."
    Invoke-BusinessOsCompose -EnvironmentName $Environment -EnvMap $envMap -ComposeArgs @('up', '-d', 'sqlserver', 'qdrant')
    $sqlContainer = ($definition.Containers | Where-Object { $_ -like '*-sqlserver-1' } | Select-Object -First 1)
    Wait-BusinessOsContainer -ContainerName $sqlContainer -TimeoutSeconds 180
}
else {
    Write-Host "Starting Qdrant for $Environment. SQL_MODE=host uses SQL Server Express and does not start a database container."
    Invoke-BusinessOsCompose -EnvironmentName $Environment -EnvMap $envMap -ComposeArgs @('up', '-d', 'qdrant')
}

Write-Host "Checking migrations for database $($envMap['SQL_DATABASE'])."
$connection = Get-MigrationConnectionString -EnvMap $envMap
$list = & dotnet ef migrations list --project (Join-Path $root 'BusinessOS.Infrastructure\BusinessOS.Infrastructure.csproj') --startup-project (Join-Path $root 'BusinessOS.API\BusinessOS.API.csproj') --configuration Release --connection $connection 2>&1
$listExit = $LASTEXITCODE
$listText = Get-RedactedText -Text ($list | Out-String)
if ($listExit -ne 0) {
    Write-Host $listText
    throw "Could not read migrations for $($envMap['SQL_DATABASE']). If this is the first start, run deploy\scripts\migrate-db.ps1 -Environment $Environment -Action update."
}

if ($listText -match '\(Pending\)') {
    Write-Host "Database $($envMap['SQL_DATABASE']) has pending migrations. The API was not started."
    Write-Host "Apply them explicitly:"
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment $Environment -Action update"
    Write-Host 'Then run this start script again.'
    exit 2
}

Write-Host "Starting the $Environment application containers."
Invoke-BusinessOsCompose -EnvironmentName $Environment -EnvMap $envMap -ComposeArgs @('up', '-d')
Write-Host "Frontend: $($definition.FrontendUrl)"
Write-Host "API:      $($definition.ApiUrl)"
if ($Environment -eq 'dev') {
    Write-Host "Scalar:   $($definition.ApiUrl)/scalar"
}
else {
    Write-Host 'Scalar is disabled for this environment.'
}

exit 0
