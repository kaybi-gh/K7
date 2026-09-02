namespace K7.Server.Domain.Common;

/// <summary>
/// Extra ids stored next to catalog format ids on
/// <c>DevicePlaybackCapabilities.SupportedMediaFormatIds</c>. Ignored by format lookup.
/// When at least one token is present the client is profile-aware and Direct Play
/// must match HEVC/AV1 Main vs Main 10, level, and decoder max resolution.
/// </summary>
public static class VideoDecoderProfileTokens
{
    public const string Prefix = "vprofile:";
    public const string HevcMain = Prefix + "hevc:main";
    public const string HevcMain10 = Prefix + "hevc:main10";
    public const string HevcDolbyVision = Prefix + "hevc:dv";
    public const string Av1Main = Prefix + "av1:main";
    public const string Av1Main10 = Prefix + "av1:main10";
    public const string LevelInfix = ":level:";
    public const string MaxInfix = ":max:";

    public static bool IsProfileAware(IEnumerable<string>? ids) =>
        ids is not null && ids.Any(id => id.StartsWith(Prefix, StringComparison.Ordinal));

    public static string Level(string codec, int level) =>
        Prefix + codec + LevelInfix + level.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string MaxResolution(string codec, int width, int height) =>
        Prefix + codec + MaxInfix + width.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "x" + height.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
