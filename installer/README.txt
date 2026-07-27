AirPods Battery Tray installers

The MSI is the conventional installer. It installs the self-contained tray app under your local application data, creates a Start Menu shortcut, and enables startup with Windows:

    AirPodsBatteryTray.msi

The PowerShell installer is a portable per-user alternative. Keep the script in the same folder as AirPodsBattery.Tray.exe and the published files.

Install with startup enabled (default):

    powershell -ExecutionPolicy Bypass -File .\Install-AirPodsBattery.ps1

Install without startup:

    powershell -ExecutionPolicy Bypass -File .\Install-AirPodsBattery.ps1 -DisableStartup

Uninstall:

    powershell -ExecutionPolicy Bypass -File .\Uninstall-AirPodsBattery.ps1
