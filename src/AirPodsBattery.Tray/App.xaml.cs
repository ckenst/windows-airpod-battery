using AirPodsBattery.Core;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace AirPodsBattery.Tray;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _notifyIcon;
    private Mutex? _singleInstanceMutex;
    private BluetoothLEAdvertisementWatcher? _watcher;
    private System.Windows.Threading.DispatcherTimer? _staleTimer;
    private int? _lastBattery;
    private DateTimeOffset? _lastSeen;
    private bool _lowBatteryNotified;
    private bool _connectionCheckInProgress;
    private AirPodsPacket? _lastPacket;
    private readonly object _pairingStateLock = new();
    private HashSet<string> _pairedAirPodsFamilies = new(StringComparer.OrdinalIgnoreCase);
    private bool _pairedAirPodsConnected;
    private DateTimeOffset _lastPairingRefresh = DateTimeOffset.MinValue;
    private int _lowBatteryThreshold = LoadThreshold();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "AirPodsBatteryTray.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(null, false, false),
            Visible = true,
            Text = "AirPods battery: waiting for data"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("AirPods battery monitor").Enabled = false;
        menu.Items.Add("Refresh scan", null, (_, _) => RestartWatcher());
        var thresholdMenu = new Forms.ToolStripMenuItem("Low-battery threshold");
        foreach (var threshold in new[] { 10, 20, 30, 40, 50 })
        {
            var item = new Forms.ToolStripMenuItem($"{threshold}%")
            {
                Tag = threshold,
                Checked = threshold == _lowBatteryThreshold,
                CheckOnClick = true
            };
            item.Click += OnThresholdSelected;
            thresholdMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(thresholdMenu);
        var startupItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = true,
            Enabled = GetStartupExecutablePath() is not null
        };
        startupItem.Click += OnStartupToggle;
        menu.Items.Add(startupItem);
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        _notifyIcon.ContextMenuStrip = menu;

        _staleTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _staleTimer.Tick += CheckForStaleData;
        _staleTimer.Start();

        StartWatcher();
        _ = RefreshPairedAirPodsStateAsync();
    }

    private void StartWatcher()
    {
        if (_watcher is not null)
            return;

        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += OnAdvertisementReceived;
        _watcher.Start();
    }

    private void RestartWatcher()
    {
        StopWatcher();
        StartWatcher();
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var manufacturer in args.Advertisement.ManufacturerData)
        {
            if (manufacturer.CompanyId != 0x004C)
                continue;

            var payload = ReadBytes(manufacturer.Data);
            var packet = AirPodsProximityParser.Parse(payload);
            if (packet is null || !packet.Model.StartsWith("AirPods", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsPacketFromPairedAirPods(packet))
                continue;

            var battery = packet.PrimaryBattery ?? packet.LeftBattery ?? packet.RightBattery ?? packet.CaseBattery;
            if (battery is not int primaryBattery)
                continue;

            Dispatcher.Invoke(() => UpdateBattery(primaryBattery, packet));
            break;
        }
    }

    private void UpdateBattery(int battery, AirPodsPacket packet)
    {
        if (_notifyIcon is null)
            return;

        _lastBattery = battery;
        _lastPacket = packet;
        _lastSeen = DateTimeOffset.UtcNow;
        _notifyIcon.Text = BuildBatteryText(packet, battery);
        SetTrayIcon(battery, true, true);

        if (battery <= _lowBatteryThreshold && !_lowBatteryNotified)
        {
            _notifyIcon.ShowBalloonTip(
                5000,
                $"{packet.Model} battery low",
                $"Battery is at {battery}% (threshold: {_lowBatteryThreshold}%).",
                Forms.ToolTipIcon.Warning);
            _lowBatteryNotified = true;
        }
            else if (battery > _lowBatteryThreshold)
        {
            _lowBatteryNotified = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_staleTimer is not null)
        {
            _staleTimer.Stop();
            _staleTimer.Tick -= CheckForStaleData;
        }
        StopWatcher();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private async void CheckForStaleData(object? sender, EventArgs e)
    {
        if (_notifyIcon is null || _connectionCheckInProgress)
            return;

        if (DateTimeOffset.UtcNow - _lastPairingRefresh > TimeSpan.FromSeconds(30))
            _ = RefreshPairedAirPodsStateAsync();

        if (_lastSeen is null || DateTimeOffset.UtcNow - _lastSeen > TimeSpan.FromSeconds(30))
        {
            _connectionCheckInProgress = true;
            var connected = await IsAirPodsConnectedAsync();
            _connectionCheckInProgress = false;

            _notifyIcon.Text = connected
                ? _lastBattery is int battery
                    ? $"{_lastPacket?.Model ?? "AirPods"}: connected — battery unavailable (last: {battery}%)"
                    : "AirPods: connected — battery unavailable"
                : "AirPods: disconnected";
            SetTrayIcon(_lastBattery, connected, false);
            _lowBatteryNotified = false;
        }
    }

    private bool IsPacketFromPairedAirPods(AirPodsPacket packet)
    {
        lock (_pairingStateLock)
        {
            // Until the first device query completes, retain the diagnostic
            // behavior and allow valid AirPods packets through.
            if (_lastPairingRefresh == DateTimeOffset.MinValue)
                return true;

            if (!_pairedAirPodsConnected || _pairedAirPodsFamilies.Count == 0)
                return false;

            return _pairedAirPodsFamilies.Contains(GetPacketFamily(packet.Model));
        }
    }

    private async Task RefreshPairedAirPodsStateAsync()
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelector());
            var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connected = false;

            foreach (var deviceInfo in devices.Where(device =>
                         device.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase)))
            {
                var family = GetAirPodsFamily(deviceInfo.Name);
                if (family is null)
                    continue;

                families.Add(family);
                using var device = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                connected |= device?.ConnectionStatus == BluetoothConnectionStatus.Connected;
            }

            lock (_pairingStateLock)
            {
                _pairedAirPodsFamilies = families;
                _pairedAirPodsConnected = connected;
                _lastPairingRefresh = DateTimeOffset.UtcNow;
            }
        }
        catch
        {
            // Leave the previous pairing state intact if Bluetooth is busy.
        }
    }

    private static string? GetAirPodsFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            !name.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
            return null;

        if (name.Contains("Max", StringComparison.OrdinalIgnoreCase))
            return "AirPods Max";
        if (name.Contains("Pro", StringComparison.OrdinalIgnoreCase))
            return "AirPods Pro";
        return "AirPods";
    }

    private static string GetPacketFamily(string model) => model switch
    {
        "AirPods Max" => "AirPods Max",
        "AirPods Pro" or "AirPods Pro 2" => "AirPods Pro",
        _ => "AirPods"
    };

    private static async Task<bool> IsAirPodsConnectedAsync()
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelector());
            foreach (var deviceInfo in devices.Where(device =>
                         device.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase)))
            {
                using var device = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                if (device?.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    return true;
            }
        }
        catch
        {
            // Treat an unavailable Bluetooth query as disconnected until the next check.
        }

        return false;
    }

    private static string BuildBatteryText(AirPodsPacket packet, int primaryBattery)
    {
        if (packet.Model == "AirPods Max")
            return $"AirPods Max: {primaryBattery}%";

        return $"{packet.Model}: L {FormatBattery(packet.LeftBattery)} R {FormatBattery(packet.RightBattery)} Case {FormatBattery(packet.CaseBattery)}";
    }

    private static string FormatBattery(int? battery) => battery is int value ? $"{value}%" : "—";

    private void OnThresholdSelected(object? sender, EventArgs e)
    {
        if (sender is not Forms.ToolStripMenuItem item || item.Tag is not int threshold)
            return;

        _lowBatteryThreshold = threshold;
        SaveThreshold(threshold);
        _lowBatteryNotified = false;

        if (item.OwnerItem is Forms.ToolStripMenuItem parent)
        {
            foreach (Forms.ToolStripMenuItem sibling in parent.DropDownItems)
                sibling.Checked = ReferenceEquals(sibling, item);
        }
    }

    private void OnStartupToggle(object? sender, EventArgs e)
    {
        if (sender is not Forms.ToolStripMenuItem item)
            return;

        var executablePath = GetStartupExecutablePath();
        if (executablePath is null)
        {
            item.Checked = false;
            return;
        }

        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            if (item.Checked)
                runKey?.SetValue("AirPodsBatteryTray", $"\"{executablePath}\"");
            else
                runKey?.DeleteValue("AirPodsBatteryTray", false);
        }
        catch
        {
            item.Checked = !item.Checked;
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null)
            return;

        _watcher.Stop();
        _watcher.Received -= OnAdvertisementReceived;
        _watcher = null;
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void SetTrayIcon(int? battery, bool connected, bool batteryAvailable)
    {
        if (_notifyIcon is null)
            return;

        var nextIcon = CreateTrayIcon(battery, connected, batteryAvailable);
        var previousIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = nextIcon;
        previousIcon?.Dispose();
    }

    private Icon CreateTrayIcon(int? battery, bool connected, bool batteryAvailable)
    {
        var referenceIconPath = Path.Combine(AppContext.BaseDirectory, "icons8-airpods-pro-max.png");
        if (File.Exists(referenceIconPath))
            return CreateReferenceTrayIcon(referenceIconPath, battery, connected, batteryAvailable);

        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var stateColor = GetStateColor(battery, connected, batteryAvailable);

        using (var background = new SolidBrush(stateColor))
            graphics.FillEllipse(background, 1, 1, 62, 62);
        using (var border = new Pen(Color.FromArgb(8, 25, 70), 2))
            graphics.DrawEllipse(border, 2, 2, 60, 60);

        using var batteryOutline = new Pen(Color.White, 4) { LineJoin = LineJoin.Round };
        graphics.DrawRectangle(batteryOutline, 12, 19, 39, 26);
        using var batteryTip = new SolidBrush(Color.White);
        graphics.FillRectangle(batteryTip, 51, 27, 5, 10);

        if (batteryAvailable && battery is int percentage)
        {
            var fillWidth = Math.Max(2, (int)Math.Round(31 * Math.Clamp(percentage, 0, 100) / 100.0));
            using var fill = new SolidBrush(Color.White);
            graphics.FillRectangle(fill, 17, 24, fillWidth, 16);
        }
        else
        {
            using var dash = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(dash, 22, 32, 41, 32);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private Icon CreateReferenceTrayIcon(string path, int? battery, bool connected, bool batteryAvailable)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using (var source = new Bitmap(path))
            graphics.DrawImage(source, new Rectangle(3, 2, 58, 58), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);

        var stateColor = GetStateColor(battery, connected, batteryAvailable);
        using var badge = new SolidBrush(stateColor);
        using var badgeOutline = new Pen(Color.White, 2);
        graphics.FillEllipse(badge, 45, 45, 16, 16);
        graphics.DrawEllipse(badgeOutline, 45, 45, 16, 16);

        if (!batteryAvailable)
        {
            using var mark = new Pen(Color.White, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(mark, 49, 53, 57, 53);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private Color GetStateColor(int? battery, bool connected, bool batteryAvailable) => !connected
        ? Color.FromArgb(107, 114, 128)
        : !batteryAvailable
            ? Color.FromArgb(37, 99, 235)
            : battery <= _lowBatteryThreshold
                ? Color.FromArgb(239, 68, 68)
                : battery <= 30
                    ? Color.FromArgb(245, 158, 11)
                    : Color.FromArgb(34, 197, 94);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AirPodsBatteryTray",
        "settings.json");

    private static string? GetStartupExecutablePath()
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "AirPodsBattery.Tray.exe");
        return File.Exists(executablePath) ? executablePath : null;
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            return runKey?.GetValue("AirPodsBatteryTray") is string value &&
                   value.Contains("AirPodsBattery.Tray.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int LoadThreshold()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<TraySettings>(File.ReadAllText(SettingsPath));
                if (settings?.LowBatteryThreshold is >= 10 and <= 50)
                    return settings.LowBatteryThreshold;
            }
        }
        catch
        {
            // Use the default if settings are unavailable or malformed.
        }

        return 30;
    }

    private static void SaveThreshold(int threshold)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new TraySettings(threshold)));
        }
        catch
        {
            // A non-persistent setting should not stop battery monitoring.
        }
    }

    private sealed record TraySettings(int LowBatteryThreshold);
}
