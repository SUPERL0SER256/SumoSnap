Stop-Process -Name "SumoSnap" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "Building first..." -ForegroundColor Yellow
& "C:\Users\Sumer\AppData\Local\Microsoft\dotnet\dotnet.exe" build

$ExePath = Join-Path $PSScriptRoot "bin\Debug\net8.0-windows\SumoSnap.exe"

Write-Host "Launching SumoSnap..." -ForegroundColor Green
Start-Process -FilePath $ExePath
Write-Host "App is running in the background. Use the system tray icon to interact with it." -ForegroundColor Cyan
