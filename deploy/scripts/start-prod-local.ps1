$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\start-env.ps1" -Environment prod-local
exit $LASTEXITCODE
