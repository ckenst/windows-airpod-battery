namespace AirPodsBattery.Tray;

internal sealed class AirPodsConnectionState
{
    private readonly HashSet<string> _families = new(StringComparer.OrdinalIgnoreCase);

    public AirPodsConnectionState(IEnumerable<(string Name, bool IsConnected)> devices)
    {
        foreach (var (name, connected) in devices)
        {
            var family = GetFamily(name);
            if (family is null || !connected)
                continue;

            _families.Add(family);
        }
    }

    public bool IsConnected => _families.Count > 0;

    public bool Accepts(string model) => _families.Contains(GetFamily(model) ?? string.Empty);

    private static string? GetFamily(string name)
    {
        if (!name.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
            return null;

        if (name.Contains("Max", StringComparison.OrdinalIgnoreCase))
            return "AirPods Max";
        if (name.Contains("Pro", StringComparison.OrdinalIgnoreCase))
            return "AirPods Pro";
        return "AirPods";
    }
}
