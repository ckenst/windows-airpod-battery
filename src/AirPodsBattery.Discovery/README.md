# AirPods Battery Discovery

This is the first discovery spike for the AirPods battery utility. It lists Bluetooth LE and classic Bluetooth devices, checks Windows battery controllers, opens a selected BLE device, enumerates GATT services and characteristics, and reads the standard Bluetooth Battery Level characteristic when available.

## Run

From PowerShell:

```powershell
dotnet run --project .\work\AirPodsBattery.Discovery\AirPodsBattery.Discovery.csproj
```

Before running:

1. Turn on Bluetooth.
2. Pair the AirPods with Windows.
3. Keep the AirPods nearby; opening the case may make their BLE services available.

The standard battery service is `0x180F`, and the battery level characteristic is `0x2A19`. If the service is absent, the next step is to investigate AirPods-specific characteristics rather than the standard reader.

## Current test result

On the development machine, AirPods Max appear as a connected classic Bluetooth audio device. They do not appear as a BLE GATT device, and Windows does not expose them as a battery controller. The standard Windows APIs therefore cannot provide their battery percentage on this machine.

The next investigation is a Bluetooth LE advertisement watcher for Apple manufacturer packets, followed by an AirPods-specific parser if the battery data is present there.

The scanner recognizes the reverse-engineered Apple proximity-pairing prefix `07 19`, labels known model bytes, and decodes battery values in 10% increments. For AirPods Max, the orientation-dependent left/right field is normalized to one battery value. Raw payloads are retained for diagnostics.
