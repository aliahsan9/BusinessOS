# Creates local env files and a localhost TLS certificate.
# Existing env files and certificates are left unchanged.

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\BusinessOsDocker.ps1"

$environments = Get-BusinessOsEnvironments
foreach ($name in @('dev', 'staging', 'prod-local')) {
    $environment = $environments[$name]
    Initialize-BusinessOsEnvFile -ExampleRelativePath $environment.Example -TargetRelativePath $environment.EnvFile
}

New-BusinessOsLocalCertificate
Write-Host 'Local Docker configuration is ready. Review deploy\README.md before starting an environment.'
exit 0
