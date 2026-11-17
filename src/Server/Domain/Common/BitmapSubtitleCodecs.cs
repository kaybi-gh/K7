namespace K7.Server.Domain.Common;

/// <summary>
/// Bitmap subtitle codecs a client can draw itself instead of asking the server to
/// burn them into a video transcode. A client declares the capability by listing
/// <see cref="Pgs"/> in <c>DevicePlaybackCapabilities.SupportedSubtitlesCodecs</c>.
/// Only embedded PGS (Blu-ray) qualifies: VobSub and DVB bitmaps are still burned in.
/// </summary>
public static class BitmapSubtitleCodecs
{
    public const string Pgs = "pgs";

    public static bool IsPgs(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
            return false;

        return codec.Trim().ToLowerInvariant() is "pgs" or "pgssub" or "hdmv_pgs_subtitle";
    }

    /// <summary>
    /// True when the device declared it renders this bitmap codec on its side.
    /// </summary>
    public static bool CanRenderOnClient(IEnumerable<string>? supportedSubtitleCodecs, string? trackCodec)
    {
        if (supportedSubtitleCodecs is null || !IsPgs(trackCodec))
            return false;

        foreach (var supported in supportedSubtitleCodecs)
        {
            if (IsPgs(supported))
                return true;
        }

        return false;
    }
}
