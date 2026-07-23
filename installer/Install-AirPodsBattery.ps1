param(
    [switch]$DisableStartup
)

$ErrorActionPreference = 'Stop'
$appName = 'AirPodsBatteryTray'
$installRoot = Join-Path $env:LOCALAPPDATA $appName
$sourceRoot = $PSScriptRoot
$exeName = 'AirPodsBattery.Tray.exe'
$sourceExe = Join-Path $sourceRoot $exeName
$targetExe = Join-Path $installRoot $exeName
$startMenuRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuRoot 'AirPods Battery Tray.lnk'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Cannot find $exeName next to this installer. Keep the installer in the published app folder."
}

Get-Process -Name 'AirPodsBattery.Tray' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -Path (Join-Path $sourceRoot '*') -Destination $installRoot -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'AirPods battery monitor'
$shortcut.Save()

New-Item -Path $runKeyPath -Force | Out-Null
if ($DisableStartup) {
    Remove-ItemProperty -Path $runKeyPath -Name $appName -ErrorAction SilentlyContinue
} else {
    Set-ItemProperty -Path $runKeyPath -Name $appName -Value ('"' + $targetExe + '"')
}

Start-Process -FilePath $targetExe -WorkingDirectory $installRoot
Write-Host "Installed to $installRoot"
Write-Host "Start with Windows: $(-not $DisableStartup)"
