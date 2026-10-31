# Enables TCP port 1433 on the existing SQLEXPRESS instance only.
# This does not change SQLEXPRESS01, SQLEXPRESS02, login mode, or any database.
# Without -Apply the script only prints the current settings.
# -Restart restarts only the MSSQL$SQLEXPRESS service and requires -Apply.

param(
    [switch]$Apply,
    [switch]$Restart
)

$ErrorActionPreference = 'Stop'

if ($Restart -and -not $Apply) {
    throw '-Restart is only valid together with -Apply.'
}

$instance = 'MSSQL16.SQLEXPRESS'
$tcpKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instance\MSSQLServer\SuperSocketNetLib\Tcp"
$ipAllKey = Join-Path $tcpKey 'IPAll'
$tcp = Get-ItemProperty $tcpKey
$ipAll = Get-ItemProperty $ipAllKey
Write-Host "SQLEXPRESS TCP enabled=$($tcp.Enabled) port='$($ipAll.TcpPort)' dynamic='$($ipAll.TcpDynamicPorts)'"

if (-not $Apply) {
    Write-Host 'No changes made. Re-run with -Apply -Restart from an elevated PowerShell to listen on port 1433.'
    exit 0
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Enabling SQL Server TCP requires an elevated PowerShell session.'
}

Set-ItemProperty -Path $tcpKey -Name Enabled -Value 1 -Type DWord
Set-ItemProperty -Path $ipAllKey -Name TcpPort -Value '1433' -Type String
Set-ItemProperty -Path $ipAllKey -Name TcpDynamicPorts -Value '' -Type String
Write-Host 'Registry updated for SQLEXPRESS only. The service must restart before the port is open.'

if (-not $Restart) {
    Write-Host 'Restart was not requested. SQL Server Express is still using the previous network settings.'
    exit 0
}

Restart-Service -Name 'MSSQL$SQLEXPRESS' -Force
Write-Host 'Restarted MSSQL$SQLEXPRESS. Other SQL Server instances were not restarted.'
Write-Host 'Create a SQL login for Docker separately. Do not point it at the BusinessOS database.'
exit 0
