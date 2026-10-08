namespace K7.Server.Application.Services;

/// <summary>
/// Tracks which 4 MiB pieces of a federated media file are present locally.
/// </summary>
public sealed class FederatedMediaPieceMap
{
    public const int PieceSizeBytes = 4 * 1024 * 1024;
    public const long HeaderBytes = 32L * 1024 * 1024;
    public const long TailBytes = 16L * 1024 * 1024;
    /// <summary>
    /// Extra bytes around a proportional time map (MKV clusters are not linearly packed).
    /// </summary>
    public const long TimeWindowMarginBytes = 16L * 1024 * 1024;

    /// <summary>
    /// Bytes that must be present before ffmpeg starts a remux window (blocking).
    /// Remux is I/O bound and can start with a modest runway.
    /// </summary>
    public const long EnsureRunwayBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Blocking bytes before a Peer encode window starts. Same size as remux:
    /// the coverage pump fills the rest while ffmpeg runs. A 512 MiB floor
    /// delayed the first frame for no extra safety once that pump is running.
    /// </summary>
    public const long EncodeEnsureRunwayBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Background prefetch ahead of the playback head (non-blocking).
    /// </summary>
    public const long PrefetchWindowBytes = 256L * 1024 * 1024;

    private readonly HashSet<int> _present = [];

    public FederatedMediaPieceMap(long contentLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        ContentLength = contentLength;
        PieceCount = contentLength == 0
            ? 0
            : (int)((contentLength + PieceSizeBytes - 1) / PieceSizeBytes);
    }

    public long ContentLength { get; }

    public int PieceCount { get; }

    public int PresentCount => _present.Count;

    public bool IsPiecePresent(int pieceIndex) =>
        pieceIndex >= 0 && pieceIndex < PieceCount && _present.Contains(pieceIndex);

    public void MarkPresent(int pieceIndex)
    {
        if (pieceIndex >= 0 && pieceIndex < PieceCount)
            _present.Add(pieceIndex);
    }

    public void MarkRangePresent(long offset, long count)
    {
        if (count <= 0 || ContentLength <= 0)
            return;

        var endExclusive = Math.Min(offset + count, ContentLength);
        if (offset >= endExclusive)
            return;

        var first = (int)(offset / PieceSizeBytes);
        var last = (int)((endExclusive - 1) / PieceSizeBytes);
        for (var i = first; i <= last; i++)
            _present.Add(i);
    }

    public IReadOnlyList<(long Offset, long Count)> MissingRanges(long offset, long count)
    {
        if (count <= 0 || ContentLength <= 0)
            return [];

        var start = Math.Clamp(offset, 0, ContentLength);
        var endExclusive = Math.Clamp(offset + count, 0, ContentLength);
        if (start >= endExclusive)
            return [];

        var first = (int)(start / PieceSizeBytes);
        var last = (int)((endExclusive - 1) / PieceSizeBytes);
        var ranges = new List<(long Offset, long Count)>();
        long? runStart = null;
        long runEnd = 0;

        for (var i = first; i <= last; i++)
        {
            var pieceStart = (long)i * PieceSizeBytes;
            var pieceEnd = Math.Min(pieceStart + PieceSizeBytes, ContentLength);
            var overlapStart = Math.Max(start, pieceStart);
            var overlapEnd = Math.Min(endExclusive, pieceEnd);
            if (overlapStart >= overlapEnd)
                continue;

            if (_present.Contains(i))
            {
                Flush(ranges, ref runStart, runEnd);
                continue;
            }

            if (runStart is null)
            {
                runStart = overlapStart;
                runEnd = overlapEnd;
            }
            else if (overlapStart <= runEnd)
            {
                runEnd = Math.Max(runEnd, overlapEnd);
            }
            else
            {
                Flush(ranges, ref runStart, runEnd);
                runStart = overlapStart;
                runEnd = overlapEnd;
            }
        }

        Flush(ranges, ref runStart, runEnd);
        return ranges;
    }

    public static (long Offset, long Count) HeaderAndCuesRange(long contentLength)
    {
        if (contentLength <= 0)
            return (0, 0);

        if (contentLength <= HeaderBytes + TailBytes)
            return (0, contentLength);

        // Caller fetches head and tail separately when they do not overlap.
        return (0, HeaderBytes);
    }

    public static (long Offset, long Count)? TailRange(long contentLength)
    {
        if (contentLength <= 0)
            return null;

        if (contentLength <= HeaderBytes + TailBytes)
            return null;

        var offset = contentLength - TailBytes;
        return (offset, TailBytes);
    }

    public static (long Offset, long Count) ApproximateTimeWindow(
        long contentLength,
        TimeSpan windowStart,
        TimeSpan windowEnd,
        TimeSpan? mediaDuration,
        long runwayBytes = EnsureRunwayBytes)
    {
        if (contentLength <= 0)
            return (0, 0);

        var runway = Math.Max(EnsureRunwayBytes, runwayBytes);

        var durationMs = mediaDuration is { TotalMilliseconds: > 0 } d
            ? d.TotalMilliseconds
            : 0;
        if (durationMs <= 0)
            return (0, Math.Min(contentLength, runway));

        var startMs = Math.Max(0, windowStart.TotalMilliseconds);
        var endMs = Math.Max(startMs, windowEnd.TotalMilliseconds);
        var startRatio = Math.Clamp(startMs / durationMs, 0, 1);
        var endRatio = Math.Clamp(endMs / durationMs, 0, 1);
        var centerStart = (long)(contentLength * startRatio);
        var centerEnd = (long)Math.Ceiling(contentLength * endRatio);
        var offset = Math.Max(0, centerStart - TimeWindowMarginBytes);
        var naturalEnd = Math.Min(contentLength, centerEnd + TimeWindowMarginBytes);
        var naturalCount = Math.Max(0, naturalEnd - offset);

        // Cap and floor the blocking fetch. The coverage pump extends it while ffmpeg runs.
        var count = Math.Min(naturalCount, runway);
        // Always keep a minimum runway ahead of the head for ffmpeg demux.
        count = Math.Max(count, Math.Min(runway, Math.Max(0, contentLength - offset)));
        count = Math.Min(count, Math.Max(0, contentLength - offset));
        return (offset, count);
    }

    /// <summary>
    /// Splits a byte range into piece-aligned chunks for HTTP Range fetches.
    /// </summary>
    public static IEnumerable<(long Offset, long Count)> ChunkRange(long offset, long count, long contentLength)
    {
        if (count <= 0 || contentLength <= 0)
            yield break;

        var start = Math.Clamp(offset, 0, contentLength);
        var endExclusive = Math.Clamp(offset + count, 0, contentLength);
        for (var cursor = start; cursor < endExclusive;)
        {
            var pieceStart = cursor - (cursor % PieceSizeBytes);
            var nextBoundary = Math.Min(pieceStart + PieceSizeBytes, contentLength);
            var chunkEnd = Math.Min(nextBoundary, endExclusive);
            var chunkCount = chunkEnd - cursor;
            if (chunkCount > 0)
                yield return (cursor, chunkCount);
            cursor = chunkEnd;
        }
    }

    public IEnumerable<int> SerializePresentPieces() => _present.OrderBy(static i => i);

    public void LoadPresentPieces(IEnumerable<int> pieceIndices)
    {
        foreach (var index in pieceIndices)
            MarkPresent(index);
    }

    private static void Flush(List<(long Offset, long Count)> ranges, ref long? runStart, long runEnd)
    {
        if (runStart is not { } start)
            return;

        ranges.Add((start, runEnd - start));
        runStart = null;
    }
}
