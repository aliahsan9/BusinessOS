# Creates a SQL Server backup of BusinessOS without touching the source database files.
# Restore is a separate, explicit step and refuses to target the source database.
param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [ValidateSet("Full", "Differential", "Log")]
    [string]$BackupType = "Full",

    [switch]$RestoreTo,

    [string]$RestoreDatabase
)

$ErrorActionPreference = "Stop"

if ($RestoreTo -and [string]::IsNullOrWhiteSpace($RestoreDatabase)) {
    throw "RestoreTo requires -RestoreDatabase."
}

if ($RestoreTo -and $RestoreDatabase -eq $Database) {
    throw "Refusing to restore over the source database '$Database'."
}

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found. Install SQL Server command-line tools before running a backup."
}

$backupDir = Split-Path -Parent $BackupPath
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
}

switch ($BackupType) {
    "Full" { $with = "INIT, CHECKSUM, STATS = 10" }
    "Differential" { $with = "DIFFERENTIAL, CHECKSUM, STATS = 10" }
    "Log" { $with = "CHECKSUM, STATS = 10" }
}

$backupSql = @"
BACKUP DATABASE [$Database]
TO DISK = N'$BackupPath'
WITH $with;
"@

if ($BackupType -eq "Log") {
    $backupSql = @"
BACKUP LOG [$Database]
TO DISK = N'$BackupPath'
WITH $with;
"@
}

Write-Host "Backing up [$Database] on $Server to $BackupPath ($BackupType)."
sqlcmd -S $Server -b -Q $backupSql
if ($LASTEXITCODE -ne 0) {
    throw "Backup failed with exit code $LASTEXITCODE."
}

sqlcmd -S $Server -b -Q "RESTORE VERIFYONLY FROM DISK = N'$BackupPath' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) {
    throw "Backup file failed VERIFYONLY. Do not treat it as restorable."
}

if (-not $RestoreTo) {
    Write-Host "Backup verified. No restore was performed."
    return
}

$restoreSql = @"
RESTORE DATABASE [$RestoreDatabase]
FROM DISK = N'$BackupPath'
WITH REPLACE, RECOVERY, STATS = 10;
DBCC CHECKDB ([$RestoreDatabase]) WITH NO_INFOMSGS;
"@

Write-Host "Restoring into separate database [$RestoreDatabase]."
sqlcmd -S $Server -b -Q $restoreSql
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code $LASTEXITCODE. The source database was not overwritten."
}

Write-Host "Restore and CHECKDB completed for [$RestoreDatabase]."
