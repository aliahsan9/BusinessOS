# Stops one environment, or all of them.
# Containers can be removed with -RemoveContainers. Named volumes are kept.
# This script never runs `docker compose down -v`.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'staging', 'prod-local', 'qdrant', 'all')]
    [string]$Environment,
    [switch]$RemoveContainers
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

function Stop-OneProject {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    $root = Get-BusinessOsRoot
    if ($Name -eq 'qdrant') {
        Write-Host 'Stopping workflow A Qdrant. Its volume qdrant_storage is kept.'
        if ($RemoveContainers) {
            & docker compose -f (Join-Path $root 'docker-compose.yml') down
        }
        else {
            & docker compose -f (Join-Path $root 'docker-compose.yml') stop
        }

        if ($LASTEXITCODE -ne 0) {
            throw 'Could not stop workflow A Qdrant.'
        }

        Write-Host 'Workflow A Qdrant is stopped. The volume was not deleted.'
        return
    }

    $definition = (Get-BusinessOsEnvironments)[$Name]
    $envPath = Join-Path $root $definition.EnvFile
    $stopped = $false
    if (Test-Path $envPath) {
        $envMap = Read-BusinessOsEnvFile -Path $envPath
        $composeArgs = @('stop')
        if ($RemoveContainers) {
            $composeArgs = @('down')
        }

        Write-Host "Stopping $Name. Persistent volumes are kept."
        try {
            Invoke-BusinessOsCompose -EnvironmentName $Name -EnvMap $envMap -ComposeArgs $composeArgs
            $stopped = $true
        }
        catch {
            Write-Host "Compose stop did not complete for $Name. Trying container names."
        }
    }

    if (-not $stopped) {
        $existing = @()
        foreach ($container in $definition.Containers) {
            & docker inspect $container | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $existing += $container
            }
        }

        if ($existing.Count -eq 0) {
            Write-Host "$Name has no running containers."
            return
        }

        if ($RemoveContainers) {
            & docker rm -f @existing
        }
        else {
            & docker stop @existing
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Could not stop $Name."
        }
    }

    if ($RemoveContainers) {
        Write-Host "$Name containers were removed. Named volumes were kept. Data is still on disk."
    }
    else {
        Write-Host "$Name containers were stopped. Volumes and containers were kept."
    }

    Write-Host 'This script does not delete volumes. docker compose down -v would delete database data; do not use it as a stop command.'
}

Assert-DockerDaemon
if ($Environment -eq 'all') {
    foreach ($name in @('dev', 'staging', 'prod-local', 'qdrant')) {
        Stop-OneProject -Name $name
    }
}
else {
    Stop-OneProject -Name $Environment
}

exit 0
