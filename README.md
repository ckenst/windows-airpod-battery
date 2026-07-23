# AirPods Battery Tray for Windows

A Windows notification-area app that displays battery information for paired AirPods, AirPods Pro, and AirPods Max devices.

## Projects

- `src/AirPodsBattery.Core` — shared AirPods advertisement parsing
- `src/AirPodsBattery.Discovery` — Bluetooth and advertisement diagnostic tool
- `src/AirPodsBattery.Tray` — tray application
- `installer` — WiX MSI and portable PowerShell installer scripts

## Build the tray app

```powershell
dotnet build .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj
dotnet run --project .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj
```

## Build a distributable MSI

Publish the self-contained app, then build the WiX package with WiX Toolset installed:

```powershell
dotnet publish .\src\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\AirPodsBatteryTray
wix build .\installer\Product.wxs -d SourceDir="$PWD\publish\AirPodsBatteryTray" -o .\AirPodsBatteryTray.msi
```

The generated MSI belongs in a GitHub Release, not in the source repository.
