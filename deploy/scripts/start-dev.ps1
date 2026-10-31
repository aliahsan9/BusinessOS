# Workflow A starts Qdrant only. Run the API and Angular on Windows for debugging.
# Workflow B starts the API, Angular, and Qdrant in Docker.

param(
    [ValidateSet('A', 'B')]
    [string]$Workflow = 'A'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

Assert-DockerDaemon
$root = Get-BusinessOsRoot
Set-Location $root

if ($Workflow -eq 'A') {
    Write-Host 'Starting workflow A: Qdrant only. The API and Angular stay on Windows.'
    & docker compose -f (Join-Path $root 'docker-compose.yml') up -d
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start the workflow A Qdrant container.'
    }

    Write-Host ''
    Write-Host 'Qdrant is starting on 127.0.0.1:6333 and 127.0.0.1:6334.'
    Write-Host 'In a second terminal, from BusinessOS:'
    Write-Host '  dotnet run --project BusinessOS.API\BusinessOS.API.csproj --launch-profile http'
    Write-Host 'In a third terminal, from BusinessOS_UI:'
    Write-Host '  npm start'
    Write-Host 'Frontend: http://localhost:4200'
    Write-Host 'API:      http://localhost:5162'
    Write-Host 'Scalar:   http://localhost:5162/scalar'
    exit 0
}

Write-Host 'Starting workflow B: API, frontend, and Qdrant in Docker.'
Write-Host 'Stop workflow A first if it is using ports 4200, 5162, or 6333.'
& "$PSScriptRoot\start-env.ps1" -Environment dev
exit $LASTEXITCODE
