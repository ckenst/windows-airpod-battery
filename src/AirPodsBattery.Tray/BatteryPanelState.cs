namespace AirPodsBattery.Tray;

internal sealed record BatteryPanelState(string Device, string Percentage, string Status, string Details)
{
    public static BatteryPanelState Waiting => new("AirPods", "—", "Waiting for data", "Connect your AirPods to see their battery level");

    public static BatteryPanelState Current(string device, int battery, string details) =>
        new(device, $"{battery}%", "Connected", details);

    public static BatteryPanelState Unavailable(string device, bool connected, int? lastBattery) =>
        new(device, "—", connected ? "Connected" : "Disconnected",
            lastBattery is int battery
                ? $"Last reported: {battery}%" + (connected ? "\nChecking for an update…" : string.Empty)
                : connected ? "Checking battery…" : "Connect your AirPods to see their battery level");
}
