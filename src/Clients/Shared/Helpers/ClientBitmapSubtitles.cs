using K7.Server.Domain.Common;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Embedded PGS drawn by the player itself, from the server .sup sidecar, instead of a
/// burn-in transcode. VobSub and DVB bitmaps are not covered and stay burned in.
/// </summary>
public static class ClientBitmapSubtitles
{
    private const string BurnInQueryKey = "SubtitleBurnInStreamIndex=";

    public static bool IsPgs(SubtitleFileTrackDto? track) =>
        track is { IsTextBased: false } && BitmapSubtitleCodecs.IsPgs(track.Codec);

    /// <summary>
    /// True when the player has to draw the selected track: PGS on an HLS stream that does
    /// not carry it burned in. Direct Play keeps the track in the file, where the native
    /// player renders it.
    /// </summary>
    public static bool ShouldDraw(SubtitleFileTrackDto? track, string? mimeType, string? url) =>
        IsPgs(track)
        && StreamingSourceKind.IsHls(mimeType, url)
        && !IsBurnInUrl(url);

    public static bool IsBurnInUrl(string? url) =>
        url?.Contains(BurnInQueryKey, StringComparison.OrdinalIgnoreCase) == true;
}
