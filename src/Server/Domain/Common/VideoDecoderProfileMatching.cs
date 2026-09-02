using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Domain.Common;

/// <summary>
/// Direct Play gate for Android MediaCodec profile/level/max-res tokens.
/// Clients that do not send <see cref="VideoDecoderProfileTokens"/> keep the old MIME-only behavior.
/// </summary>
public static class VideoDecoderProfileMatching
{
    public static bool AllowsDirectPlay(IEnumerable<string>? formatIds, VideoFileTrack track)
    {
        var ids = formatIds as ICollection<string> ?? formatIds?.ToList() ?? [];
        if (!VideoDecoderProfileTokens.IsProfileAware(ids))
            return true;

        var codec = MediaCodecNames.Canonical(track.Codec);
        if (codec is "hevc")
            return AllowsHevc(ids, track);
        if (codec is "av1")
            return AllowsAv1(ids, track);

        return true;
    }

    public static bool IsTenBit(VideoFileTrack track)
    {
        if (track.BitDepth is >= 10)
            return true;

        var profile = track.Profile;
        if (string.IsNullOrWhiteSpace(profile))
            return false;

        return profile.Contains("10", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDolbyVision(VideoFileTrack track)
    {
        var codec = track.Codec ?? "";
        if (codec.Contains("dolby", StringComparison.OrdinalIgnoreCase)
            || codec.StartsWith("dvhe", StringComparison.OrdinalIgnoreCase)
            || codec.StartsWith("dvh1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var profile = track.Profile ?? "";
        return profile.Contains("dvhe", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("dvh1", StringComparison.OrdinalIgnoreCase)
            || profile.Contains("dolby", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsHevc(ICollection<string> ids, VideoFileTrack track)
    {
        if (IsDolbyVision(track) && !ids.Contains(VideoDecoderProfileTokens.HevcDolbyVision))
            return false;

        if (IsTenBit(track))
        {
            if (!ids.Contains(VideoDecoderProfileTokens.HevcMain10))
                return false;
        }
        else if (!ids.Contains(VideoDecoderProfileTokens.HevcMain)
            && !ids.Contains(VideoDecoderProfileTokens.HevcMain10))
        {
            return false;
        }

        return FitsLevelAndSize(ids, "hevc", track);
    }

    private static bool AllowsAv1(ICollection<string> ids, VideoFileTrack track)
    {
        if (IsTenBit(track))
        {
            if (!ids.Contains(VideoDecoderProfileTokens.Av1Main10))
                return false;
        }
        else if (!ids.Contains(VideoDecoderProfileTokens.Av1Main)
            && !ids.Contains(VideoDecoderProfileTokens.Av1Main10))
        {
            return false;
        }

        return FitsLevelAndSize(ids, "av1", track);
    }

    private static bool FitsLevelAndSize(ICollection<string> ids, string codec, VideoFileTrack track)
    {
        var maxLevel = ReadMaxLevel(ids, codec);
        if (maxLevel is > 0 && track.Level > 0)
        {
            var fileLevel = codec is "hevc"
                ? HlsCodecStringHelpers.NormalizeHevcLevelIdc(track.Level)
                : track.Level;
            if (fileLevel > maxLevel)
                return false;
        }

        var (maxW, maxH) = ReadMaxSize(ids, codec);
        if (maxW > 0 && maxH > 0 && track.Width > 0 && track.Height > 0)
        {
            if (track.Width > maxW || track.Height > maxH)
                return false;
        }

        return true;
    }

    private static int? ReadMaxLevel(ICollection<string> ids, string codec)
    {
        var prefix = VideoDecoderProfileTokens.Prefix + codec + VideoDecoderProfileTokens.LevelInfix;
        var best = 0;
        foreach (var id in ids)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (int.TryParse(id.AsSpan(prefix.Length), out var level) && level > best)
                best = level;
        }

        return best > 0 ? best : null;
    }

    private static (int Width, int Height) ReadMaxSize(ICollection<string> ids, string codec)
    {
        var prefix = VideoDecoderProfileTokens.Prefix + codec + VideoDecoderProfileTokens.MaxInfix;
        var bestW = 0;
        var bestH = 0;
        foreach (var id in ids)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rest = id[prefix.Length..];
            var x = rest.IndexOf('x');
            if (x <= 0 || x == rest.Length - 1)
                continue;
            if (!int.TryParse(rest.AsSpan(0, x), out var w)
                || !int.TryParse(rest.AsSpan(x + 1), out var h))
            {
                continue;
            }

            if (w * h > bestW * bestH)
            {
                bestW = w;
                bestH = h;
            }
        }

        return (bestW, bestH);
    }
}
