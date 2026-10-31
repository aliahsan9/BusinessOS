$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\start-env.ps1" -Environment staging
exit $LASTEXITCODE
