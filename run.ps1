$ExePath = Join-Path $PSScriptRoot "bin\Debug\net8.0-windows\SumoSnap.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "Building first..." -ForegroundColor Yellow
    & "C:\Users\Sumer\AppData\Local\Microsoft\dotnet\dotnet.exe" build
}

Write-Host "Launching AI Screenshot Utility..." -ForegroundColor Green
Start-Process -FilePath $ExePath
Write-Host "App is running in the background. Use the system tray icon to interact with it." -ForegroundColor Cyan
