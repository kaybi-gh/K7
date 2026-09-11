namespace K7.Server.Domain.Helpers;

/// <summary>
/// Drops open-GOP keyframes that are unsafe as ffmpeg <c>-f segment</c> cut points.
/// After such a CRA in decode order, trailing packets with lower PTS are discarded by
/// the segment muxer, which punches multi-frame holes into remux playlists.
/// </summary>
public static class HlsOpenGopKeyframeFilter
{
    public static List<long> FilterSafeKeyframeTimestamps(
        IEnumerable<(long PtsMs, bool IsKeyframe)> packetsInDecodeOrder)
    {
        var safe = new List<long>();
        long? pendingPts = null;
        var pendingUnsafe = false;

        foreach (var (ptsMs, isKeyframe) in packetsInDecodeOrder)
        {
            if (isKeyframe)
            {
                if (pendingPts is long previous && !pendingUnsafe)
                    safe.Add(previous);

                pendingPts = ptsMs;
                pendingUnsafe = false;
                continue;
            }

            if (pendingPts is long keyPts && ptsMs < keyPts)
                pendingUnsafe = true;
        }

        if (pendingPts is long last && !pendingUnsafe)
            safe.Add(last);

        return safe;
    }
}
