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
