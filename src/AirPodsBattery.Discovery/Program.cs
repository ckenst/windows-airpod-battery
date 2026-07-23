using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Power;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth.Advertisement;

const string batteryServiceUuid = "0000180f-0000-1000-8000-00805f9b34fb";
const string batteryLevelUuid = "00002a19-0000-1000-8000-00805f9b34fb";

Console.WriteLine("AirPods Battery — Bluetooth GATT discovery");
Console.WriteLine(new string('-', 48));

await PrintWindowsBatteryControllersAsync();
Console.WriteLine();

var devices = new List<(DeviceInformation Info, string Transport)>();
try
{
    var bleDevices = await DeviceInformation.FindAllAsync(
        BluetoothLEDevice.GetDeviceSelector(),
        RequestedProperties());
    devices.AddRange(bleDevices.Select(device => (device, "BLE")));

    var classicDevices = await DeviceInformation.FindAllAsync(
        BluetoothDevice.GetDeviceSelector(),
        RequestedProperties());
    foreach (var device in classicDevices)
    {
        if (devices.All(existing => !existing.Info.Id.Equals(device.Id, StringComparison.OrdinalIgnoreCase)))
            devices.Add((device, "classic"));
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not enumerate Bluetooth devices: {ex.Message}");
    return 1;
}

if (devices.Count == 0)
{
    Console.WriteLine("No Bluetooth LE devices were found. Make sure Bluetooth is enabled and the AirPods are paired.");
    return 0;
}

Console.WriteLine("Paired/available Bluetooth LE devices:");
for (var i = 0; i < devices.Count; i++)
{
    var device = devices[i];
    var paired = GetBooleanProperty(device.Info, "System.Devices.Aep.IsPaired");
    var connected = GetBooleanProperty(device.Info, "System.Devices.Aep.IsConnected");
    var name = string.IsNullOrWhiteSpace(device.Info.Name) ? "(unnamed)" : device.Info.Name;
    Console.WriteLine($"  [{i}] {name} | transport={device.Transport} paired={paired} connected={connected}");
}

Console.WriteLine();
Console.WriteLine("Enter 's' to scan Apple BLE advertisements, or select a device number.");
Console.Write("Selection (or press Enter to exit): ");
var selection = Console.ReadLine();
if (string.Equals(selection, "s", StringComparison.OrdinalIgnoreCase))
{
    await ScanAppleAdvertisementsAsync(TimeSpan.FromSeconds(20));
    return 0;
}

if (!int.TryParse(selection, out var index) || index < 0 || index >= devices.Count)
    return 0;

var selected = devices[index];
Console.WriteLine($"\nOpening {selected.Info.Name}...");

if (!selected.Transport.Equals("BLE", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Windows properties for this device:");
    foreach (var property in selected.Info.Properties.Where(property =>
                 property.Key.Contains("Battery", StringComparison.OrdinalIgnoreCase) ||
                 property.Key.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"  {property.Key} = {property.Value ?? "(null)"}");
    }

    Console.WriteLine("This is a classic Bluetooth entry, not a BLE entry.");
    Console.WriteLine("The audio connection can be active while Windows does not expose a GATT data endpoint for it.");
    Console.WriteLine("If you have a separate BLE entry for the AirPods, select that entry instead.");
    return 0;
}

BluetoothLEDevice? bleDevice = null;
try
{
    bleDevice = await BluetoothLEDevice.FromIdAsync(selected.Info.Id);
    if (bleDevice is null)
    {
        Console.WriteLine("Windows did not return a Bluetooth LE device. Try pairing the AirPods first.");
        return 1;
    }

    var servicesResult = await bleDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
    if (servicesResult.Status != GattCommunicationStatus.Success)
    {
        Console.WriteLine($"GATT service discovery failed: {servicesResult.Status}");
        Console.WriteLine("The device may be connected for audio but not currently exposing its BLE data.");
        return 1;
    }

    Console.WriteLine($"Found {servicesResult.Services.Count} GATT service(s):");
    foreach (var service in servicesResult.Services)
    {
        Console.WriteLine($"\nService: {service.Uuid}");

        var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (characteristicsResult.Status != GattCommunicationStatus.Success)
        {
            Console.WriteLine($"  Characteristic discovery failed: {characteristicsResult.Status}");
            continue;
        }

        foreach (var characteristic in characteristicsResult.Characteristics)
        {
            Console.WriteLine($"  Characteristic: {characteristic.Uuid}");
            Console.WriteLine($"    Properties: {characteristic.CharacteristicProperties}");

            if (characteristic.Uuid.ToString().Equals(batteryLevelUuid, StringComparison.OrdinalIgnoreCase))
            {
                var value = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (value.Status == GattCommunicationStatus.Success)
                {
                    var bytes = ReadBytes(value.Value);
                    if (bytes.Length > 0)
                        Console.WriteLine($"    Battery level: {bytes[0]}%");
                }
                else
                {
                    Console.WriteLine($"    Battery read failed: {value.Status}");
                }
            }
        }
    }

    var standardBattery = servicesResult.Services.FirstOrDefault(
        service => service.Uuid.ToString().Equals(batteryServiceUuid, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine();
    Console.WriteLine(standardBattery is null
        ? "Result: the standard Bluetooth Battery Service was not found. AirPods-specific discovery may be required."
        : "Result: the standard Bluetooth Battery Service is present.");
}
catch (UnauthorizedAccessException)
{
    Console.WriteLine("Windows denied access. Confirm the AirPods are paired and available to this app.");
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Bluetooth operation failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}
finally
{
    bleDevice?.Dispose();
}

return 0;

static bool GetBooleanProperty(DeviceInformation device, string key)
{
    return device.Properties.TryGetValue(key, out var value) && value is bool boolean && boolean;
}

static string[] RequestedProperties() =>
[
    "System.Devices.Aep.IsPaired",
    "System.Devices.Aep.IsConnected"
];

static byte[] ReadBytes(IBuffer buffer)
{
    using var reader = DataReader.FromBuffer(buffer);
    var bytes = new byte[reader.UnconsumedBufferLength];
    reader.ReadBytes(bytes);
    return bytes;
}

static async Task PrintWindowsBatteryControllersAsync()
{
    Console.WriteLine("Windows battery controllers:");
    DeviceInformationCollection batteries;
    try
    {
        batteries = await DeviceInformation.FindAllAsync(Battery.GetDeviceSelector());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Battery enumeration failed: {ex.Message}");
        return;
    }

    if (batteries.Count == 0)
    {
        Console.WriteLine("  None reported.");
        return;
    }

    foreach (var device in batteries)
    {
        try
        {
            var battery = await Battery.FromIdAsync(device.Id);
            var report = battery?.GetReport();
            Console.WriteLine($"  {device.Name}: status={report?.Status} remaining={report?.RemainingCapacityInMilliwattHours}mWh full={report?.FullChargeCapacityInMilliwattHours}mWh");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {device.Name}: unavailable ({ex.Message})");
        }
    }
}

static async Task ScanAppleAdvertisementsAsync(TimeSpan duration)
{
    Console.WriteLine();
    Console.WriteLine($"Scanning for Apple BLE advertisements for {duration.TotalSeconds:0} seconds...");
    Console.WriteLine("Keep the AirPods nearby and open the case or remove an earbud while scanning.");

    using var completed = new CancellationTokenSource(duration);
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var watcher = new BluetoothLEAdvertisementWatcher
    {
        ScanningMode = BluetoothLEScanningMode.Active
    };

    void OnAdvertisement(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var manufacturer in args.Advertisement.ManufacturerData)
        {
            // Apple, Inc. Bluetooth SIG company identifier.
            if (manufacturer.CompanyId != 0x004C)
                continue;

            var payload = ReadBytes(manufacturer.Data);
            var name = string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
                ? "(no local name)"
                : args.Advertisement.LocalName;
            var key = $"{args.BluetoothAddress:X12}:{Convert.ToHexString(payload)}";
            if (!seen.Add(key))
                continue;

            var candidate = DescribeAirPodsCandidate(payload);
            var parsed = ParseAirPodsPacket(payload);

            Console.WriteLine();
            Console.WriteLine($"Apple advertisement{(candidate is null ? "" : $" — {candidate}")}: {name}");
            Console.WriteLine($"  Address: {args.BluetoothAddress:X12}");
            Console.WriteLine($"  RSSI: {args.RawSignalStrengthInDBm} dBm");
            Console.WriteLine($"  Payload ({payload.Length} bytes): {Convert.ToHexString(payload)}");
            if (parsed is not null)
            {
                Console.WriteLine($"  Decoded model: {parsed.Model}");
                if (parsed.Model == "AirPods Max")
                {
                    var battery = parsed.LeftBattery is int left && parsed.RightBattery is int right
                        ? Math.Max(left, right)
                        : parsed.LeftBattery ?? parsed.RightBattery;
                    Console.WriteLine($"  Battery: {FormatBattery(battery)}");
                    Console.WriteLine($"  Charging: {parsed.LeftCharging || parsed.RightCharging}");
                }
                else
                {
                    Console.WriteLine($"  Battery fields: left={FormatBattery(parsed.LeftBattery)} right={FormatBattery(parsed.RightBattery)} case={FormatBattery(parsed.CaseBattery)}");
                    Console.WriteLine($"  Charging flags: left={parsed.LeftCharging} right={parsed.RightCharging} case={parsed.CaseCharging}");
                }
            }
        }
    }

    watcher.Received += OnAdvertisement;
    watcher.Start();
    try
    {
        await Task.Delay(duration, completed.Token);
    }
    catch (OperationCanceledException)
    {
        // Expected when the scan duration expires.
    }
    finally
    {
        watcher.Stop();
        watcher.Received -= OnAdvertisement;
    }

    Console.WriteLine();
    Console.WriteLine(seen.Count == 0
        ? "No Apple manufacturer advertisements were observed."
        : $"Observed {seen.Count} unique Apple advertisement payload(s). Raw payloads are logged for the next parser step.");
}

static string? DescribeAirPodsCandidate(byte[] payload)
{
    // Apple proximity-pairing advertisements use type 0x07, subtype 0x19.
    // The model mapping is reverse-engineered and remains a candidate label
    // until a known-good battery fixture is used to validate the parser.
    if (payload.Length < 8 || payload[0] != 0x07 || payload[1] != 0x19)
        return null;

    return payload[3] switch
    {
        0x0A => "likely AirPods Max proximity packet",
        0x0E => "likely AirPods Pro proximity packet",
        0x13 => "likely AirPods 3 proximity packet",
        0x14 => "likely AirPods Pro 2 proximity packet",
        _ => $"possible AirPods proximity packet (model byte 0x{payload[3]:X2})"
    };
}

static AirPodsPacket? ParseAirPodsPacket(byte[] payload)
{
    // Payload is manufacturer data after Apple's company identifier.
    // The layout follows the reverse-engineered AirStatus proximity-pairing frame.
    if (payload.Length != 27 || payload[0] != 0x07)
        return null;

    var hex = Convert.ToHexString(payload).ToLowerInvariant();
    var model = hex[7] switch
    {
        '2' => "AirPods (1st gen)",
        'f' => "AirPods (2nd gen)",
        '3' => "AirPods (3rd gen)",
        'e' => "AirPods Pro",
        '4' => "AirPods Pro 2",
        'a' => "AirPods Max",
        _ => "AirPods"
    };

    var flipped = (HexNibble(hex[10]) & 0x02) == 0;
    var leftIndex = flipped ? 13 : 12;
    var rightIndex = flipped ? 12 : 13;

    var leftBattery = DecodeBatteryNibble(HexNibble(hex[leftIndex]));
    var rightBattery = DecodeBatteryNibble(HexNibble(hex[rightIndex]));
    var charge = HexNibble(hex[14]);
    var caseBattery = DecodeBatteryNibble(HexNibble(hex[15]));

    var leftCharging = (charge & (flipped ? 0b0010 : 0b0001)) != 0;
    var rightCharging = (charge & (flipped ? 0b0001 : 0b0010)) != 0;
    var caseCharging = (charge & 0b0100) != 0;

    return new AirPodsPacket(model, leftBattery, rightBattery, caseBattery, leftCharging, rightCharging, caseCharging);
}

static int? DecodeBatteryNibble(int nibble) => nibble <= 10 ? nibble * 10 : null;

static string FormatBattery(int? battery) => battery is null ? "unknown" : $"{battery}%";

static int HexNibble(char value) => value switch
{
    >= '0' and <= '9' => value - '0',
    >= 'a' and <= 'f' => value - 'a' + 10,
    _ => throw new FormatException($"Invalid hexadecimal character: {value}")
};

record AirPodsPacket(
    string Model,
    int? LeftBattery,
    int? RightBattery,
    int? CaseBattery,
    bool LeftCharging,
    bool RightCharging,
    bool CaseCharging);
