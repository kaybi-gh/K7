using System.Numerics;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Pairwise chromaprint matching: inverted index, contiguous runs,
/// and bridging of neighbouring runs so noisy openings still form one intro.
/// </summary>
public static class ChromaprintFingerprintMatcher
{
    public const int MaxFingerprintPointDifferences = 6;
    public const double MaxTimeSkipSeconds = 3.5;
    public const int InvertedIndexShift = 2;
    public const double SampleDuration = 4096.0 / 11025.0 / 3.0;

    /// <summary>
    /// Neighbours on each side (episode order) that are always compared, so the longest match
    /// among them wins even when the first one is already valid.
    /// </summary>
    public const int NeighborRadius = 2;

    /// <summary>
    /// Upper bound of partners tried for one episode, nearest first. Neighbours often share the
    /// same cut of the opening, but a season that alternates openings, or drops them on some
    /// episodes, only matches farther away.
    /// </summary>
    public const int MaxComparisonPartners = 48;

    /// <summary>Partners that must agree on the same region before a short match is trusted.</summary>
    public const int ShortMatchConsensusPartners = 3;

    /// <summary>How far apart two short matches may start and end and still be the same region.</summary>
    public const double ShortMatchToleranceSeconds = 2.0;

    private const double BridgeMinimumRunDuration = 8.0;
    private const double BridgeMaximumGapShare = 0.5;

    public readonly record struct FingerprintMatch(
        double LhsStartSeconds,
        double LhsEndSeconds,
        double RhsStartSeconds,
        double RhsEndSeconds);

    public readonly record struct SegmentMatch(double StartSeconds, double EndSeconds);

    /// <summary>
    /// Best shared region per episode. <paramref name="fingerprints"/> must be in episode order
    /// (same index = same episode).
    /// </summary>
    /// <param name="shortMinDurationSeconds">
    /// When set, a region shorter than <paramref name="minDurationSeconds"/> but at least this long
    /// is kept if <see cref="ShortMatchConsensusPartners"/> partners (or every partner, in a smaller
    /// season of at least three episodes) share the same region: a title card of a few seconds is
    /// only an intro when the whole season repeats it.
    /// </param>
    /// <param name="targets">Indexes to resolve; null resolves every episode.</param>
    public static Dictionary<int, SegmentMatch> FindBestMatches(
        IReadOnlyList<uint[]?> fingerprints,
        double minDurationSeconds,
        double maxDurationSeconds,
        double? shortMinDurationSeconds = null,
        IReadOnlySet<int>? targets = null,
        CancellationToken cancellationToken = default)
    {
        var bestMatches = new Dictionary<int, SegmentMatch>();
        var pairCache = new Dictionary<(int, int), FingerprintMatch?>();

        for (var i = 0; i < fingerprints.Count; i++)
        {
            if (targets is not null && !targets.Contains(i))
                continue;

            var fpI = fingerprints[i];
            if (fpI is null || fpI.Length == 0)
                continue;

            SegmentMatch? best = null;
            var shortCandidates = new List<SegmentMatch>();
            var partners = 0;

            foreach (var j in PartnersNearestFirst(i, fingerprints.Count))
            {
                var fpJ = fingerprints[j];
                if (fpJ is null || fpJ.Length == 0)
                    continue;

                if (partners >= MaxComparisonPartners)
                    break;

                // Past the neighbours, the first valid match is enough.
                if (best is not null && Math.Abs(j - i) > NeighborRadius)
                    break;

                cancellationToken.ThrowIfCancellationRequested();
                partners++;

                var region = CompareCached(pairCache, fingerprints, i, j);
                if (region is null)
                    continue;

                var duration = region.Value.EndSeconds - region.Value.StartSeconds;
                if (duration > maxDurationSeconds)
                    continue;

                if (duration >= minDurationSeconds)
                {
                    if (best is null || duration > best.Value.EndSeconds - best.Value.StartSeconds)
                        best = region;
                }
                else if (shortMinDurationSeconds is not null && duration >= shortMinDurationSeconds.Value)
                {
                    shortCandidates.Add(region.Value);
                }
            }

            best ??= FindShortConsensus(shortCandidates, partners);
            if (best is not null)
                bestMatches[i] = best.Value;
        }

        return bestMatches;
    }

    private static IEnumerable<int> PartnersNearestFirst(int index, int count)
    {
        for (var distance = 1; distance < count; distance++)
        {
            if (index + distance < count)
                yield return index + distance;
            if (index - distance >= 0)
                yield return index - distance;
        }
    }

    /// <summary>Region of episode <paramref name="i"/> shared with episode <paramref name="j"/>.</summary>
    private static SegmentMatch? CompareCached(
        Dictionary<(int, int), FingerprintMatch?> cache,
        IReadOnlyList<uint[]?> fingerprints,
        int i,
        int j)
    {
        var key = i < j ? (i, j) : (j, i);
        if (!cache.TryGetValue(key, out var match))
        {
            match = CompareFingerprints(fingerprints[key.Item1]!, fingerprints[key.Item2]!);
            cache[key] = match;
        }

        if (match is null)
            return null;

        return i < j
            ? new SegmentMatch(match.Value.LhsStartSeconds, match.Value.LhsEndSeconds)
            : new SegmentMatch(match.Value.RhsStartSeconds, match.Value.RhsEndSeconds);
    }

    private static SegmentMatch? FindShortConsensus(List<SegmentMatch> candidates, int partners)
    {
        var required = Math.Min(ShortMatchConsensusPartners, partners);
        if (required < 2 || candidates.Count < required)
            return null;

        List<SegmentMatch>? bestGroup = null;
        foreach (var pivot in candidates)
        {
            var group = candidates
                .Where(c => Math.Abs(c.StartSeconds - pivot.StartSeconds) <= ShortMatchToleranceSeconds
                    && Math.Abs(c.EndSeconds - pivot.EndSeconds) <= ShortMatchToleranceSeconds)
                .ToList();
            if (bestGroup is null || group.Count > bestGroup.Count)
                bestGroup = group;
        }

        if (bestGroup is null || bestGroup.Count < required)
            return null;

        return new SegmentMatch(
            Median(bestGroup.Select(c => c.StartSeconds)),
            Median(bestGroup.Select(c => c.EndSeconds)));
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    public static FingerprintMatch? CompareFingerprints(uint[] lhs, uint[] rhs)
    {
        if (lhs.Length == 0 || rhs.Length == 0)
            return null;

        var rhsIndex = CreateInvertedIndex(rhs);
        var candidateShifts = FindCandidateShifts(lhs, rhsIndex);
        FingerprintMatch? best = null;
        var bestDuration = 0.0;

        foreach (var shift in candidateShifts)
        {
            var result = FindContiguousAtShift(lhs, rhs, shift);
            if (result is null)
                continue;

            var duration = result.Value.LhsEndSeconds - result.Value.LhsStartSeconds;
            if (duration > bestDuration)
            {
                bestDuration = duration;
                best = result;
            }
        }

        return best;
    }

    public static FingerprintMatch? FindContiguousAtShift(uint[] lhs, uint[] rhs, int shift)
    {
        var lhsOffset = Math.Max(0, shift);
        var rhsOffset = Math.Max(0, -shift);
        var overlapLength = Math.Min(lhs.Length - lhsOffset, rhs.Length - rhsOffset);
        if (overlapLength <= 0)
            return null;

        var runs = new List<(int Start, int End)>();
        for (var i = 0; i < overlapLength; i++)
        {
            var lhsPos = lhsOffset + i;
            var diff = BitOperations.PopCount(lhs[lhsPos] ^ rhs[rhsOffset + i]);
            if (diff > MaxFingerprintPointDifferences)
                continue;

            if (runs.Count > 0
                && (lhsPos - runs[^1].End) * SampleDuration <= MaxTimeSkipSeconds)
            {
                runs[^1] = (runs[^1].Start, lhsPos);
            }
            else
            {
                runs.Add((lhsPos, lhsPos));
            }
        }

        if (runs.Count == 0)
            return null;

        BridgeRuns(runs);

        var bestStart = -1;
        var bestEnd = -1;
        foreach (var (start, end) in runs)
        {
            if (bestStart < 0 || end - start > bestEnd - bestStart)
                (bestStart, bestEnd) = (start, end);
        }

        if (bestStart < 0)
            return null;

        var positionShift = rhsOffset - lhsOffset;
        return new FingerprintMatch(
            LhsStartSeconds: bestStart * SampleDuration,
            LhsEndSeconds: (bestEnd + 1) * SampleDuration,
            RhsStartSeconds: (bestStart + positionShift) * SampleDuration,
            RhsEndSeconds: (bestEnd + 1 + positionShift) * SampleDuration);
    }

    private static void BridgeRuns(List<(int Start, int End)> runs)
    {
        var joined = true;
        while (joined)
        {
            joined = false;
            for (var i = 0; i + 1 < runs.Count; i++)
            {
                if (!Bridges(runs[i], runs[i + 1]))
                    continue;

                runs[i] = (runs[i].Start, runs[i + 1].End);
                runs.RemoveAt(i + 1);
                joined = true;
            }
        }
    }

    private static bool Bridges((int Start, int End) left, (int Start, int End) right)
    {
        var shorter = Math.Min(left.End - left.Start, right.End - right.Start) * SampleDuration;
        if (shorter < BridgeMinimumRunDuration)
            return false;

        var gap = (right.Start - left.End) * SampleDuration;
        return gap <= shorter * BridgeMaximumGapShare;
    }

    private static Dictionary<uint, int> CreateInvertedIndex(uint[] fingerprint)
    {
        var index = new Dictionary<uint, int>(fingerprint.Length);
        for (var i = 0; i < fingerprint.Length; i++)
            index[fingerprint[i]] = i;
        return index;
    }

    private static HashSet<int> FindCandidateShifts(uint[] lhs, Dictionary<uint, int> rhsIndex)
    {
        var shifts = new HashSet<int> { 0 };

        for (var i = 0; i < lhs.Length; i++)
        {
            var value = lhs[i];
            for (var delta = -InvertedIndexShift; delta <= InvertedIndexShift; delta++)
            {
                var target = unchecked((uint)((long)value + delta));
                if (rhsIndex.TryGetValue(target, out var rhsPos))
                    shifts.Add(i - rhsPos);
            }
        }

        return shifts;
    }
}
