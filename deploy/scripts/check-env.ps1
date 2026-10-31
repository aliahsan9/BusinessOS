# Reports local Docker, port, disk, and SQL Server Express readiness.
# Exit code 1 means Docker cannot run containers yet. SQL TCP being disabled is reported
# and does not by itself fail the check, because the optional SQL container is available.

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

$failed = $false
Write-Host 'BusinessOS local environment check'
Write-Host ''

$dotnet = & dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "dotnet SDK: $dotnet"
}
else {
    Write-Host 'dotnet SDK: missing'
    $failed = $true
}

$node = & node --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Node.js: $node"
}
else {
    Write-Host 'Node.js: missing (required for workflow A)'
}

try {
    Assert-DockerDaemon
    $server = & docker version --format '{{.Server.Version}}'
    Write-Host "Docker daemon: $server"
}
catch {
    Write-Host 'Docker daemon: not running. Start Docker Desktop and wait until it is ready.'
    $failed = $true
}

$drive = Get-PSDrive -Name C
$freeGb = [math]::Round($drive.Free / 1GB, 1)
Write-Host "C: free space: $freeGb GB"
if ($freeGb -lt 8) {
    Write-Host 'C: has limited free space. Avoid the optional SQL Server image until more space is available.'
}

$os = Get-CimInstance Win32_OperatingSystem
$freeRam = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
Write-Host "Free RAM: $freeRam GB. Run one environment at a time on this machine."

$tcp = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp'
$ipAll = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp\IPAll'
$login = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer'
Write-Host "SQL Server Express (SQLEXPRESS) login mode: $($login.LoginMode) (2 means mixed mode)"
Write-Host "SQL Server Express TCP enabled: $($tcp.Enabled) (0 means disabled)"
Write-Host "SQL Server Express TCP port: '$($ipAll.TcpPort)' dynamic: '$($ipAll.TcpDynamicPorts)'"
if ([int]$tcp.Enabled -ne 1) {
    Write-Host 'Containers cannot reach SQL Server Express until TCP is enabled. Use SQL_MODE=container, or run deploy\scripts\enable-sql-express-tcp.ps1 -Apply -Restart from an elevated PowerShell.'
}

$ports = @(
    @{ Name = 'workflow A/B frontend'; Port = 4200 },
    @{ Name = 'staging frontend'; Port = 4201 },
    @{ Name = 'prod-local frontend'; Port = 4202 },
    @{ Name = 'workflow A/B API'; Port = 5162 },
    @{ Name = 'staging API'; Port = 5102 },
    @{ Name = 'prod-local API'; Port = 5103 },
    @{ Name = 'workflow A/B Qdrant'; Port = 6333 },
    @{ Name = 'dev SQL container'; Port = 14331 },
    @{ Name = 'staging SQL container'; Port = 14332 },
    @{ Name = 'prod-local SQL container'; Port = 14333 }
)

foreach ($entry in $ports) {
    $listening = Get-NetTCPConnection -LocalPort $entry.Port -State Listen -ErrorAction SilentlyContinue
    if ($listening) {
        Write-Host "Port $($entry.Port) ($($entry.Name)) is in use."
    }
    else {
        Write-Host "Port $($entry.Port) ($($entry.Name)) is free."
    }
}

$root = Get-BusinessOsRoot
foreach ($name in @('dev', 'staging', 'prod-local')) {
    $relative = (Get-BusinessOsEnvironments)[$name].EnvFile
    $path = Join-Path $root $relative
    if (Test-Path $path) {
        Write-Host "Env file present: $relative"
    }
    else {
        Write-Host "Env file missing: $relative. Run deploy\scripts\setup-local.ps1."
    }
}

if ($failed) {
    exit 1
}

exit 0
