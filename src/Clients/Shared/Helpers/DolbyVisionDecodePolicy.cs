namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Device-local Dolby Vision decode path for ExoPlayer. Profile 8.1 is HEVC + RPU with
/// an HDR10 base layer. Asking MediaCodec for video/dolby-vision can hitch on some
/// Android TV HALs even when the same HEVC decoder is smooth.
/// </summary>
public enum DolbyVisionDecodeMode
{
    Native,
    HevcHdr10
}

public static class DolbyVisionDecodePolicy
{
    public const string Native = "native";
    public const string HevcHdr10 = "hevc";

    public static DolbyVisionDecodeMode? TryParse(string? stored) => stored?.Trim().ToLowerInvariant() switch
    {
        Native => DolbyVisionDecodeMode.Native,
        HevcHdr10 => DolbyVisionDecodeMode.HevcHdr10,
        _ => null
    };

    public static string Persist(DolbyVisionDecodeMode mode) =>
        mode == DolbyVisionDecodeMode.Native ? Native : HevcHdr10;

    /// <summary>
    /// Android TV defaults to HEVC/HDR10 so Profile 8 Direct Play skips the DV MIME path.
    /// Phones keep native Dolby Vision.
    /// </summary>
    public static DolbyVisionDecodeMode DefaultForDevice(
        bool isTelevision,
        string? manufacturer,
        string? model)
    {
        if (isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model))
            return DolbyVisionDecodeMode.HevcHdr10;

        return DolbyVisionDecodeMode.Native;
    }

    public static DolbyVisionDecodeMode Resolve(
        string? stored,
        bool isTelevision,
        string? manufacturer,
        string? model) =>
        TryParse(stored) ?? DefaultForDevice(isTelevision, manufacturer, model);
}
