namespace AirPodsBattery.Core;

public sealed record AirPodsPacket(
    string Model,
    int? LeftBattery,
    int? RightBattery,
    int? CaseBattery,
    bool LeftCharging,
    bool RightCharging,
    bool CaseCharging)
{
    public int? PrimaryBattery => Model == "AirPods Max"
        ? LeftBattery is int left && RightBattery is int right
            ? Math.Max(left, right)
            : LeftBattery ?? RightBattery
        : null;
}

public static class AirPodsProximityParser
{
    public static AirPodsPacket? Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 27 || payload[0] != 0x07 || payload[1] != 0x19)
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
        var leftBattery = Decode(HexNibble(hex[leftIndex]));
        var rightBattery = Decode(HexNibble(hex[rightIndex]));
        var charge = HexNibble(hex[14]);
        var caseBattery = Decode(HexNibble(hex[15]));

        return new AirPodsPacket(
            model,
            leftBattery,
            rightBattery,
            caseBattery,
            (charge & (flipped ? 0b0010 : 0b0001)) != 0,
            (charge & (flipped ? 0b0001 : 0b0010)) != 0,
            (charge & 0b0100) != 0);
    }

    private static int? Decode(int nibble) => nibble <= 10 ? nibble * 10 : null;

    private static int HexNibble(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        _ => throw new FormatException($"Invalid hexadecimal character: {value}")
    };
}
