$ErrorActionPreference = 'Stop'
$appName = 'AirPodsBatteryTray'
$installRoot = Join-Path $env:LOCALAPPDATA $appName
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\AirPods Battery Tray.lnk'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Get-Process -Name 'AirPodsBattery.Tray' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path $runKeyPath -Name $appName -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}

Write-Host 'AirPods Battery Tray was uninstalled.'
