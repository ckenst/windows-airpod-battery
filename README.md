# AirPods Battery Tray for Windows

A Windows notification-area app that displays battery information for paired AirPods, AirPods Pro, and AirPods Max devices.

Left-click the tray icon to open the battery panel. It shows the current percentage and connection status, or the last reported reading when fresh battery data is unavailable. Click elsewhere or press Escape to dismiss it. Right-click the icon for settings, refresh, and exit. Battery information is shown in the panel instead of a hover tooltip.

## Projects

- `src/AirPodsBattery.Core` — shared AirPods advertisement parsing
- `src/AirPodsBattery.Discovery` — Bluetooth and advertisement diagnostic tool
- `src/AirPodsBattery.Tray` — tray application
- `installer` — WiX MSI and portable PowerShell installer scripts

## Build the tray app

Install a current .NET 10 SDK before building. Self-contained publishes use the latest runtime patch supplied by that SDK.

```powershell
dotnet build .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj
dotnet run --project .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj
```

## Build a distributable MSI

Publish the self-contained app, then build the WiX package with WiX Toolset installed:

```powershell
dotnet publish .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\AirPodsBatteryTray
Copy-Item .\src\AirPodsBattery.Tray\airpods-tray.ico .\publish\AirPodsBatteryTray\ -Force
Copy-Item .\src\AirPodsBattery.Tray\icons8-airpods-pro-max.png .\publish\AirPodsBatteryTray\ -Force
wix build .\installer\Product.wxs -d SourceDir="$PWD\publish\AirPodsBatteryTray" -o .\AirPodsBatteryTray.msi
```

The generated MSI belongs in a GitHub Release, not in the source repository.
