namespace K7.Server.Domain.Common;

public static class AudioPassthroughCodecs
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "ac3", "eac3", "dts", "truehd", "mlp", "atmos"
    };

    public static bool IsPassthrough(string? codec) =>
        !string.IsNullOrWhiteSpace(codec) && Names.Contains(MediaCodecNames.Canonical(codec));
}
