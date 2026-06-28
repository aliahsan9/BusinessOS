# Stops any running BusinessOS.API instance, then starts the API.
# Use this instead of bare `dotnet run` to avoid MSB3027 file-lock build errors.

$existing = Get-Process -Name "BusinessOS.API" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing BusinessOS.API process(es)..."
    $existing | Stop-Process -Force
    Start-Sleep -Seconds 2
}

dotnet run @args
