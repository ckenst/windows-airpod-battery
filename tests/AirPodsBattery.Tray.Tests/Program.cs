using AirPodsBattery.Tray;

static void Equal(string expected, string actual)
{
    if (expected != actual)
        throw new Exception($"Expected '{expected}', got '{actual}'");
}

var waiting = BatteryPanelState.Waiting;
Equal("Waiting for data", waiting.Status);
Equal("—", waiting.Percentage);
var fresh = BatteryPanelState.Current("AirPods Max", 75, "AirPods Max: 75%");
Equal("75%", fresh.Percentage);
Equal("AirPods Max: 75%", fresh.Details);
Equal("Connected", fresh.Status);
var stale = BatteryPanelState.Unavailable("AirPods Max", true, 75);
Equal("—", stale.Percentage);
Equal("Connected", stale.Status);
Equal("Last reported: 75%\nChecking for an update…", stale.Details);
var unknown = BatteryPanelState.Unavailable("AirPods", true, null);
Equal("Connected", unknown.Status);
Equal("—", unknown.Percentage);
Equal("Checking battery…", unknown.Details);
var disconnected = BatteryPanelState.Unavailable("AirPods Max", false, 75);
Equal("Disconnected", disconnected.Status);
Equal("—", disconnected.Percentage);
Equal("Last reported: 75%", disconnected.Details);
var disconnectedWithoutReading = BatteryPanelState.Unavailable("AirPods", false, null);
Equal("Disconnected", disconnectedWithoutReading.Status);
Equal("Connect your AirPods to see their battery level", disconnectedWithoutReading.Details);
Console.WriteLine("Passed: waiting, current, stale, unavailable, and disconnected panel states.");

static void Accepts(AirPodsConnectionState state, string model, bool expected)
{
    if (state.Accepts(model) != expected)
        throw new Exception($"Expected acceptance of '{model}' to be {expected}.");
}

var maxConnected = new AirPodsConnectionState(new[]
{
    ("My AirPods Max", true),
    ("My AirPods Pro 2", false)
});
Accepts(maxConnected, "AirPods Max", true);
Accepts(maxConnected, "AirPods Pro 2", false);
Console.WriteLine("Passed: disconnected Pro 2 broadcasts cannot replace connected Max readings.");

var proConnected = new AirPodsConnectionState(new[]
{
    ("My AirPods Max", false),
    ("My AIRPODS PRO 2", true)
});
Accepts(proConnected, "AirPods Pro 2", true);
Accepts(proConnected, "AirPods Max", false);
Accepts(proConnected, "AirPods (3rd gen)", false);

var disconnectedDevices = new AirPodsConnectionState(new[]
{
    ("My AirPods Max", false),
    ("My AirPods Pro 2", false),
    ("Other headphones", true)
});
Accepts(disconnectedDevices, "AirPods Max", false);
Accepts(disconnectedDevices, "AirPods Pro 2", false);
Equal("False", disconnectedDevices.IsConnected.ToString());

var startup = new AirPodsConnectionState(Array.Empty<(string, bool)>());
Accepts(startup, "AirPods Max", false);
Accepts(startup, "AirPods Pro 2", false);

var bothConnected = new AirPodsConnectionState(new[]
{
    ("AirPods Max", true),
    ("AirPods Pro", true),
    ("AirPods", true)
});
Accepts(bothConnected, "AirPods Max", true);
Accepts(bothConnected, "AirPods Pro", true);
Accepts(bothConnected, "AirPods Pro 2", true);
Accepts(bothConnected, "AirPods (2nd gen)", true);
Equal("True", bothConnected.IsConnected.ToString());
Console.WriteLine("Passed: connection switching, startup, all disconnected, unrelated devices, and connected model families.");
