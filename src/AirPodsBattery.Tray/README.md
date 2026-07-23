# AirPods Battery Tray

AirPods Battery Tray runs in the Windows notification area, watches Apple BLE advertisements, and shows battery data for paired AirPods families, including AirPods, AirPods Pro, and AirPods Max. It only accepts live readings from a paired AirPods family when Windows reports that family as connected.

Run from the workspace root:

```powershell
dotnet run --project .\work\AirPodsBattery.Tray\AirPodsBattery.Tray.csproj
```

Right-click the tray icon to refresh the scan, choose a low-battery threshold, or exit. The default warning threshold is 30%, and the setting is saved under Windows local application data. For models that advertise separate values, the tooltip shows left, right, and case percentages. If no battery advertisement arrives, the app distinguishes between `connected — battery unavailable` and `disconnected`, then automatically resumes the live percentage when the headset broadcasts again.
