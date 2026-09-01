param (
    [switch]$AutoStart = $false
)

Write-Host "Installing SumoSnap..." -ForegroundColor Cyan

# 1. Define install paths
$InstallDir = "$env:LOCALAPPDATA\SumoSnap"
$ExePath = "$InstallDir\SumoSnap.exe"

# 2. Create the directory if it doesn't exist
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# 3. Copy the executable to the local AppData folder
$SourceExe = Join-Path $PSScriptRoot "SumoSnap.exe"
if (-not (Test-Path $SourceExe)) {
    Write-Host "Error: SumoSnap.exe not found in this folder. Please extract the zip completely." -ForegroundColor Red
    Pause
    exit
}

Copy-Item $SourceExe -Destination $ExePath -Force

# 4. Create Start Menu Shortcut
$WshShell = New-Object -comObject WScript.Shell
$StartMenu = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$Shortcut = $WshShell.CreateShortcut("$StartMenu\SumoSnap.lnk")
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "AI-Powered Screenshot Utility"
$Shortcut.Save()

# 5. (Optional) Add to Windows Startup
if ($AutoStart) {
    $StartupFolder = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
    $StartupShortcut = $WshShell.CreateShortcut("$StartupFolder\SumoSnap.lnk")
    $StartupShortcut.TargetPath = $ExePath
    $StartupShortcut.WorkingDirectory = $InstallDir
    $StartupShortcut.Save()
    Write-Host "Added SumoSnap to Windows Startup!" -ForegroundColor Green
}

Write-Host "✅ Installation Complete! You can now launch SumoSnap from your Start Menu." -ForegroundColor Green
Write-Host "Press any key to close..."
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
