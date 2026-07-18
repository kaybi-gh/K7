using System.Numerics;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;

public record DetectMediaSegmentsCommand : IRequest
{
    public required Guid SeasonId { get; init; }
}

public class DetectMediaSegmentsCommandHandler : IRequestHandler<DetectMediaSegmentsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IChromaprintService _chromaprintService;
    private readonly ISegmentDetectionService _segmentDetectionService;
    private readonly ISender _sender;
    private readonly ILogger<DetectMediaSegmentsCommandHandler> _logger;

    // Fingerprint comparison (from intro-skipper)
    private const int MaxFingerprintPointDifferences = 6;
    private const double MaxTimeSkipSeconds = 3.5;
    private const int InvertedIndexShift = 2;
    private const double SampleDuration = 4096.0 / 11025.0 / 3.0;

    // Segment duration limits (seconds)
    private const double MinIntroDurationSeconds = 15;
    private const double MaxIntroDurationSeconds = 120;
    private const double MinOutroDurationSeconds = 15;
    private const double MaxOutroDurationSeconds = 450;

    // Analysis window
    private const double AnalysisLengthLimitSeconds = 600;
    private const double AnalysisPercent = 0.25;

    // Boundary adjustment
    private const double SnapToStartThresholdSeconds = 5.0;
    private const double SnapToEndThresholdSeconds = 2.0;
    private const double AdjustWindowInwardSeconds = 5.0;
    private const double AdjustWindowOutwardSeconds = 2.0;

    public DetectMediaSegmentsCommandHandler(
        IApplicationDbContext context,
        IChromaprintService chromaprintService,
        ISegmentDetectionService segmentDetectionService,
        ISender sender,
        ILogger<DetectMediaSegmentsCommandHandler> logger)
    {
        _context = context;
        _chromaprintService = chromaprintService;
        _segmentDetectionService = segmentDetectionService;
        _sender = sender;
        _logger = logger;
    }

    public async Task Handle(DetectMediaSegmentsCommand request, CancellationToken cancellationToken)
    {
        var season = await _context.Medias
            .AsNoTracking()
            .OfType<SerieSeason>()
            .Include(s => s.Episodes)
                .ThenInclude(e => e.IndexedFiles)
                    .ThenInclude(f => f.FileMetadata)
            .Include(s => s.Episodes)
                .ThenInclude(e => e.Segments)
            .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);

        if (season is null)
        {
            _logger.LogWarning("Season {SeasonId} not found for segment detection", request.SeasonId);
            return;
        }

        var episodes = season.Episodes
            .Where(e => e.IndexedFiles.Any(f => f.FileMetadata is not null && File.Exists(f.Path)))
            .OrderBy(e => e.EpisodeNumber)
            .ToList();

        if (episodes.Count < 2)
        {
            _logger.LogDebug("Season {SeasonId} has fewer than 2 episodes, skipping detection", request.SeasonId);
            return;
        }

        var hasExistingSegments = episodes.Any(e => e.Segments.Count > 0);

        List<MediaSegment> newSegments;
        if (hasExistingSegments)
            newSegments = await RunIncrementalDetectionAsync(episodes, cancellationToken);
        else
            newSegments = await RunFullDetectionAsync(episodes, cancellationToken);

        if (newSegments.Count > 0)
        {
            // Remove existing detected segments for episodes that will get new ones
            var episodeIds = newSegments.Select(s => s.MediaId).Distinct().ToList();
            var segmentTypes = newSegments.Select(s => s.Type).Distinct().ToList();

            await _context.MediaSegments
                .Where(s => episodeIds.Contains(s.MediaId) && segmentTypes.Contains(s.Type))
                .ExecuteDeleteAsync(cancellationToken);

            _context.MediaSegments.AddRange(newSegments);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await TryQueueThemeExtractAsync(season.SerieId, cancellationToken);
    }

    private async Task TryQueueThemeExtractAsync(Guid serieId, CancellationToken cancellationToken)
    {
        var themeGenerationEnabled = await (
            from episode in _context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join file in _context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            join library in _context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where library.ThemeSongGenerationEnabled && library.IntroDetectionEnabled
            select library.Id
        ).AnyAsync(cancellationToken);

        if (!themeGenerationEnabled)
            return;

        var hasIntro = await (
            from episode in _context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join segment in _context.MediaSegments.AsNoTracking() on episode.Id equals segment.MediaId
            where segment.Type == MediaSegmentType.Intro
            select segment.Id
        ).AnyAsync(cancellationToken);

        if (!hasIntro)
            return;

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new ExtractSerieThemeSongCommand { SerieId = serieId },
            Priority = BackgroundTaskPriority.Lowest,
            TargetEntityId = serieId,
            TargetEntityTypeName = nameof(Serie),
            MaxAttempts = 2,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }

    private async Task<List<MediaSegment>> RunFullDetectionAsync(List<SerieEpisode> episodes, CancellationToken cancellationToken)
    {
        var segments = new List<MediaSegment>();
        var episodeData = await ExtractAllFingerprintsAsync(episodes, cancellationToken);

        if (episodeData.Count < 2)
        {
            _logger.LogWarning("Only {Count} episodes with valid fingerprints, need at least 2", episodeData.Count);
            return segments;
        }

        LogFingerprintDiagnostics(episodeData);

        var introMatches = FindBestMatches(episodeData, isOutro: false, _logger);
        var outroMatches = FindBestMatches(episodeData, isOutro: true, _logger);

        _logger.LogInformation(
            "Found intros for {IntroCount}/{Total} and outros for {OutroCount}/{Total} episodes",
            introMatches.Count, episodeData.Count, outroMatches.Count, episodeData.Count);

        foreach (var data in episodeData)
        {
            if (introMatches.TryGetValue(data.Episode.Id, out var introMatch))
            {
                var (start, end) = await AdjustSegmentBoundariesAsync(
                    data.File.Path, introMatch.StartSeconds, introMatch.EndSeconds,
                    data.DurationSeconds, cancellationToken);

                segments.Add(new MediaSegment
                {
                    Id = Guid.NewGuid(),
                    MediaId = data.Episode.Id,
                    Type = MediaSegmentType.Intro,
                    StartMs = (long)(start * 1000),
                    EndMs = (long)(end * 1000),
                    DetectedAt = DateTimeOffset.UtcNow
                });
            }

            if (outroMatches.TryGetValue(data.Episode.Id, out var outroMatch))
            {
                var outroOffset = data.DurationSeconds - GetAnalysisWindowSeconds(data.DurationSeconds);
                var absoluteStart = outroOffset + outroMatch.StartSeconds;
                var absoluteEnd = outroOffset + outroMatch.EndSeconds;

                var (start, end) = await AdjustSegmentBoundariesAsync(
                    data.File.Path, absoluteStart, absoluteEnd,
                    data.DurationSeconds, cancellationToken);

                segments.Add(new MediaSegment
                {
                    Id = Guid.NewGuid(),
                    MediaId = data.Episode.Id,
                    Type = MediaSegmentType.Outro,
                    StartMs = (long)(start * 1000),
                    EndMs = (long)(end * 1000),
                    DetectedAt = DateTimeOffset.UtcNow
                });
            }
        }

        return segments;
    }

    private void LogFingerprintDiagnostics(List<EpisodeData> episodeData)
    {
        var introLengths = episodeData
            .Where(e => e.IntroFingerprint is not null)
            .Select(e => e.IntroFingerprint!.Length)
            .ToList();
        var outroLengths = episodeData
            .Where(e => e.OutroFingerprint is not null)
            .Select(e => e.OutroFingerprint!.Length)
            .ToList();

        _logger.LogInformation(
            "Fingerprint extraction: {Total} episodes, intros: {IntroCount} (min={IntroMin}, max={IntroMax}), outros: {OutroCount} (min={OutroMin}, max={OutroMax})",
            episodeData.Count,
            introLengths.Count,
            introLengths.Count > 0 ? introLengths.Min() : 0,
            introLengths.Count > 0 ? introLengths.Max() : 0,
            outroLengths.Count,
            outroLengths.Count > 0 ? outroLengths.Min() : 0,
            outroLengths.Count > 0 ? outroLengths.Max() : 0);

        // Diagnostic: PopCount distribution for first pair at shift=0
        if (episodeData.Count >= 2)
        {
            var fp1 = episodeData[0].IntroFingerprint;
            var fp2 = episodeData[1].IntroFingerprint;
            if (fp1 is not null && fp2 is not null)
            {
                var overlapLen = Math.Min(fp1.Length, fp2.Length);
                var popCounts = new int[33]; // 0-32 bits
                for (var i = 0; i < overlapLen; i++)
                {
                    var pc = BitOperations.PopCount(fp1[i] ^ fp2[i]);
                    popCounts[pc]++;
                }

                var matchingAtThreshold = popCounts.Take(MaxFingerprintPointDifferences + 1).Sum();
                _logger.LogInformation(
                    "Diagnostic ep1 vs ep2 at shift=0: overlap={Overlap}, matching (popcount<={Threshold}): {Matching}/{Total} ({Pct:F1}%), popcount[0]={P0}, [1-3]={P13}, [4-6]={P46}, [7-10]={P710}, [11+]={P11}",
                    overlapLen, MaxFingerprintPointDifferences,
                    matchingAtThreshold, overlapLen,
                    overlapLen > 0 ? 100.0 * matchingAtThreshold / overlapLen : 0,
                    popCounts[0],
                    popCounts[1] + popCounts[2] + popCounts[3],
                    popCounts[4] + popCounts[5] + popCounts[6],
                    popCounts[7] + popCounts[8] + popCounts[9] + popCounts[10],
                    popCounts.Skip(11).Sum());
            }
        }
    }

    private async Task<List<MediaSegment>> RunIncrementalDetectionAsync(List<SerieEpisode> episodes, CancellationToken cancellationToken)
    {
        var segments = new List<MediaSegment>();
        var withSegments = episodes.Where(e => e.Segments.Count > 0).ToList();
        var withoutSegments = episodes.Where(e => e.Segments.Count == 0).ToList();

        if (withoutSegments.Count == 0)
            return segments;

        var referenceData = await ExtractAllFingerprintsAsync(withSegments, cancellationToken);
        var newData = await ExtractAllFingerprintsAsync(withoutSegments, cancellationToken);

        if (referenceData.Count == 0 || newData.Count == 0)
            return segments;

        foreach (var newEp in newData)
        {
            var introSegment = await TryDetectSegmentFromReferencesAsync(
                newEp, referenceData, MediaSegmentType.Intro, cancellationToken);
            if (introSegment is not null)
                segments.Add(introSegment);

            var outroSegment = await TryDetectSegmentFromReferencesAsync(
                newEp, referenceData, MediaSegmentType.Outro, cancellationToken);
            if (outroSegment is not null)
                segments.Add(outroSegment);
        }

        return segments;
    }

    private async Task<MediaSegment?> TryDetectSegmentFromReferencesAsync(
        EpisodeData newEp,
        List<EpisodeData> references,
        MediaSegmentType segmentType,
        CancellationToken cancellationToken)
    {
        var isOutro = segmentType == MediaSegmentType.Outro;
        var newFp = isOutro ? newEp.OutroFingerprint : newEp.IntroFingerprint;
        if (newFp is null)
            return null;

        var minDuration = isOutro ? MinOutroDurationSeconds : MinIntroDurationSeconds;
        var maxDuration = isOutro ? MaxOutroDurationSeconds : MaxIntroDurationSeconds;

        SegmentMatch? best = null;
        var bestDuration = 0.0;

        foreach (var refEp in references)
        {
            var refFp = isOutro ? refEp.OutroFingerprint : refEp.IntroFingerprint;
            if (refFp is null)
                continue;

            var match = CompareFingerprints(refFp, newFp);
            if (match is null)
                continue;

            var duration = match.Value.RhsEndSeconds - match.Value.RhsStartSeconds;
            if (duration < minDuration || duration > maxDuration)
                continue;

            if (duration > bestDuration)
            {
                bestDuration = duration;
                best = new SegmentMatch(match.Value.RhsStartSeconds, match.Value.RhsEndSeconds);
            }
        }

        if (best is null)
            return null;

        var startSeconds = best.Value.StartSeconds;
        var endSeconds = best.Value.EndSeconds;

        if (isOutro)
        {
            var outroOffset = newEp.DurationSeconds - GetAnalysisWindowSeconds(newEp.DurationSeconds);
            startSeconds += outroOffset;
            endSeconds += outroOffset;
        }

        var (start, end) = await AdjustSegmentBoundariesAsync(
            newEp.File.Path, startSeconds, endSeconds,
            newEp.DurationSeconds, cancellationToken);

        return new MediaSegment
        {
            Id = Guid.NewGuid(),
            MediaId = newEp.Episode.Id,
            Type = segmentType,
            StartMs = (long)(start * 1000),
            EndMs = (long)(end * 1000),
            DetectedAt = DateTimeOffset.UtcNow
        };
    }

    // -- Fingerprint extraction --

    private async Task<List<EpisodeData>> ExtractAllFingerprintsAsync(
        List<SerieEpisode> episodes,
        CancellationToken cancellationToken)
    {
        var result = new List<EpisodeData>();

        foreach (var episode in episodes)
        {
            var file = episode.IndexedFiles.FirstOrDefault(f => f.FileMetadata is not null && File.Exists(f.Path));
            if (file is null)
                continue;

            var durationSeconds = GetDurationSeconds(file);
            if (durationSeconds <= 0)
                continue;

            var analysisWindow = GetAnalysisWindowSeconds(durationSeconds);

            var introFpBytes = await _chromaprintService.ExtractFingerprintAsync(
                file.Path, TimeSpan.Zero, TimeSpan.FromSeconds(analysisWindow), cancellationToken);

            var outroStart = durationSeconds - analysisWindow;
            var outroFpBytes = await _chromaprintService.ExtractFingerprintAsync(
                file.Path, TimeSpan.FromSeconds(outroStart), TimeSpan.FromSeconds(analysisWindow), cancellationToken);

            var introFp = introFpBytes is not null ? ConvertToUints(introFpBytes) : null;
            var outroFp = outroFpBytes is not null ? ConvertToUints(outroFpBytes) : null;

            if (introFp is null && outroFp is null)
                continue;

            result.Add(new EpisodeData(episode, file, durationSeconds, introFp, outroFp));
        }

        return result;
    }

    // -- All-pairs matching --

    private static Dictionary<Guid, SegmentMatch> FindBestMatches(
        List<EpisodeData> episodes,
        bool isOutro,
        ILogger logger)
    {
        var bestMatches = new Dictionary<Guid, SegmentMatch>();
        var minDuration = isOutro ? MinOutroDurationSeconds : MinIntroDurationSeconds;
        var maxDuration = isOutro ? MaxOutroDurationSeconds : MaxIntroDurationSeconds;
        var label = isOutro ? "outro" : "intro";
        var comparisonsAttempted = 0;
        var comparisonsWithMatch = 0;

        for (var i = 0; i < episodes.Count; i++)
        {
            var fpI = isOutro ? episodes[i].OutroFingerprint : episodes[i].IntroFingerprint;
            if (fpI is null)
                continue;

            for (var j = i + 1; j < episodes.Count; j++)
            {
                var fpJ = isOutro ? episodes[j].OutroFingerprint : episodes[j].IntroFingerprint;
                if (fpJ is null)
                    continue;

                comparisonsAttempted++;
                var match = CompareFingerprints(fpI, fpJ, logger, comparisonsAttempted <= 3 ? label : null);
                if (match is null)
                    continue;

                comparisonsWithMatch++;
                TryUpdateBestMatch(bestMatches, episodes[i].Episode.Id,
                    match.Value.LhsStartSeconds, match.Value.LhsEndSeconds, minDuration, maxDuration);
                TryUpdateBestMatch(bestMatches, episodes[j].Episode.Id,
                    match.Value.RhsStartSeconds, match.Value.RhsEndSeconds, minDuration, maxDuration);
            }
        }

        logger.LogInformation(
            "FindBestMatches ({Label}): {WithMatch}/{Attempted} comparisons produced a match",
            label, comparisonsWithMatch, comparisonsAttempted);

        return bestMatches;
    }

    private static void TryUpdateBestMatch(
        Dictionary<Guid, SegmentMatch> matches,
        Guid episodeId,
        double startSeconds,
        double endSeconds,
        double minDuration,
        double maxDuration)
    {
        var duration = endSeconds - startSeconds;
        if (duration < minDuration || duration > maxDuration)
            return;

        if (!matches.TryGetValue(episodeId, out var existing) ||
            duration > existing.EndSeconds - existing.StartSeconds)
        {
            matches[episodeId] = new SegmentMatch(startSeconds, endSeconds);
        }
    }

    // -- Core fingerprint comparison (inverted index approach from intro-skipper) --

    private static FingerprintMatch? CompareFingerprints(uint[] lhs, uint[] rhs, ILogger? logger = null, string? diagLabel = null)
    {
        if (lhs.Length == 0 || rhs.Length == 0)
        {
            if (diagLabel is not null)
                logger?.LogWarning("CompareFingerprints ({Label}): empty fingerprint - lhs={LhsLen}, rhs={RhsLen}", diagLabel, lhs.Length, rhs.Length);
            return null;
        }

        var rhsIndex = CreateInvertedIndex(rhs);
        var candidateShifts = FindCandidateShifts(lhs, rhsIndex);

        if (diagLabel is not null)
        {
            logger?.LogInformation(
                "CompareFingerprints ({Label}): lhs={LhsLen}, rhs={RhsLen}, rhsIndex={IndexSize}, candidates={ShiftCount}",
                diagLabel, lhs.Length, rhs.Length, rhsIndex.Count, candidateShifts.Count);
        }

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

        if (diagLabel is not null)
        {
            if (best is not null)
                logger?.LogInformation(
                    "CompareFingerprints ({Label}): best match {Duration:F2}s at lhs[{Start:F2}..{End:F2}]",
                    diagLabel, bestDuration, best.Value.LhsStartSeconds, best.Value.LhsEndSeconds);
            else
                logger?.LogWarning("CompareFingerprints ({Label}): no match found across {ShiftCount} shifts", diagLabel, candidateShifts.Count);
        }

        return best;
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
        var shifts = new HashSet<int>();

        // Always try direct alignment
        shifts.Add(0);

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

    private static FingerprintMatch? FindContiguousAtShift(uint[] lhs, uint[] rhs, int shift)
    {
        var lhsOffset = Math.Max(0, shift);
        var rhsOffset = Math.Max(0, -shift);
        var overlapLength = Math.Min(lhs.Length - lhsOffset, rhs.Length - rhsOffset);

        if (overlapLength <= 0)
            return null;

        var matchIndices = new List<int>();
        for (var i = 0; i < overlapLength; i++)
        {
            var diff = BitOperations.PopCount(lhs[lhsOffset + i] ^ rhs[rhsOffset + i]);
            if (diff <= MaxFingerprintPointDifferences)
                matchIndices.Add(i);
        }

        if (matchIndices.Count == 0)
            return null;

        var (runStart, runEnd) = FindLongestContiguousRun(matchIndices);
        if (runStart < 0)
            return null;

        var firstIdx = matchIndices[runStart];
        var lastIdx = matchIndices[runEnd];

        return new FingerprintMatch(
            LhsStartSeconds: (lhsOffset + firstIdx) * SampleDuration,
            LhsEndSeconds: (lhsOffset + lastIdx + 1) * SampleDuration,
            RhsStartSeconds: (rhsOffset + firstIdx) * SampleDuration,
            RhsEndSeconds: (rhsOffset + lastIdx + 1) * SampleDuration);
    }

    private static (int StartIndex, int EndIndex) FindLongestContiguousRun(List<int> matchIndices)
    {
        if (matchIndices.Count == 0)
            return (-1, -1);

        var bestStart = 0;
        var bestEnd = 0;
        var currentStart = 0;

        for (var i = 1; i < matchIndices.Count; i++)
        {
            var timeDelta = (matchIndices[i] - matchIndices[i - 1]) * SampleDuration;
            if (timeDelta > MaxTimeSkipSeconds)
                currentStart = i;

            if (i - currentStart > bestEnd - bestStart)
            {
                bestStart = currentStart;
                bestEnd = i;
            }
        }

        return (bestStart, bestEnd);
    }

    // -- Boundary adjustment --

    private async Task<(double StartSeconds, double EndSeconds)> AdjustSegmentBoundariesAsync(
        string filePath,
        double startSeconds,
        double endSeconds,
        double totalDurationSeconds,
        CancellationToken cancellationToken)
    {
        var adjustedStart = startSeconds;
        var adjustedEnd = endSeconds;

        var chapters = await _segmentDetectionService.GetChapterTimesAsync(filePath, cancellationToken);

        // Adjust start: snap to beginning or nearest chapter
        if (adjustedStart <= SnapToStartThresholdSeconds)
        {
            adjustedStart = 0;
        }
        else
        {
            var chapterSnap = FindNearestInWindow(chapters,
                adjustedStart,
                adjustedStart - AdjustWindowOutwardSeconds,
                adjustedStart + AdjustWindowInwardSeconds);
            if (chapterSnap is not null)
                adjustedStart = chapterSnap.Value;
        }

        // Adjust end: snap to end, or chapter, or silence, or black frame, or keyframe
        if (adjustedEnd >= totalDurationSeconds - SnapToEndThresholdSeconds)
        {
            adjustedEnd = totalDurationSeconds;
        }
        else
        {
            var chapterSnap = FindNearestInWindow(chapters,
                adjustedEnd,
                adjustedEnd - AdjustWindowInwardSeconds,
                adjustedEnd + AdjustWindowOutwardSeconds);

            if (chapterSnap is not null)
            {
                adjustedEnd = chapterSnap.Value;
            }
            else
            {
                var windowLow = adjustedEnd - AdjustWindowInwardSeconds;
                var windowHigh = adjustedEnd + AdjustWindowOutwardSeconds;
                var ffmpegStart = Math.Max(0, windowLow);
                var ffmpegDuration = windowHigh - ffmpegStart;

                // Try silence
                var silences = await _segmentDetectionService.DetectSilenceAsync(
                    filePath, ffmpegStart, ffmpegDuration, cancellationToken);
                var silenceSnap = FindFirstSilenceInWindow(silences, windowLow, windowHigh);

                if (silenceSnap is not null)
                {
                    adjustedEnd = silenceSnap.Value;
                }
                else
                {
                    // Try black frames
                    var blackFrames = await _segmentDetectionService.DetectBlackFrameTimestampsAsync(
                        filePath, ffmpegStart, ffmpegDuration, cancellationToken);
                    var blackFrameSnap = FindNearestInWindow(blackFrames, adjustedEnd, windowLow, windowHigh);

                    if (blackFrameSnap is not null)
                    {
                        adjustedEnd = blackFrameSnap.Value;
                    }
                    else
                    {
                        // Try keyframes
                        var keyframes = await _segmentDetectionService.DetectKeyframeTimestampsAsync(
                            filePath, ffmpegStart, ffmpegDuration, cancellationToken);
                        var keyframeSnap = FindNearestInWindow(keyframes, adjustedEnd, windowLow, windowHigh);
                        if (keyframeSnap is not null)
                            adjustedEnd = keyframeSnap.Value;
                    }
                }
            }
        }

        if (adjustedStart >= adjustedEnd)
            return (startSeconds, endSeconds);

        return (adjustedStart, adjustedEnd);
    }

    private static double? FindNearestInWindow(
        IReadOnlyList<double> timestamps,
        double target,
        double windowStart,
        double windowEnd)
    {
        double? nearest = null;
        var minDistance = double.MaxValue;

        foreach (var t in timestamps)
        {
            if (t < windowStart || t > windowEnd)
                continue;

            var distance = Math.Abs(t - target);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = t;
            }
        }

        return nearest;
    }

    private static double? FindFirstSilenceInWindow(
        IReadOnlyList<SilencePeriod> silences,
        double windowStart,
        double windowEnd)
    {
        foreach (var silence in silences)
        {
            if (silence.StartSeconds >= windowStart && silence.StartSeconds <= windowEnd)
                return silence.StartSeconds;
        }

        return null;
    }

    // -- Helpers --

    private static uint[] ConvertToUints(byte[] data)
    {
        var count = data.Length / sizeof(uint);
        var result = new uint[count];
        Buffer.BlockCopy(data, 0, result, 0, count * sizeof(uint));
        return result;
    }

    private static double GetDurationSeconds(IndexedFile file)
    {
        return file.FileMetadata switch
        {
            VideoFileMetadata v => v.Duration.TotalSeconds,
            AudioFileMetadata a => a.Duration.TotalSeconds,
            _ => 0
        };
    }

    private static double GetAnalysisWindowSeconds(double totalDurationSeconds)
    {
        return Math.Min(totalDurationSeconds * AnalysisPercent, AnalysisLengthLimitSeconds);
    }

    // -- Inner types --

    private readonly record struct FingerprintMatch(
        double LhsStartSeconds, double LhsEndSeconds,
        double RhsStartSeconds, double RhsEndSeconds);

    private readonly record struct SegmentMatch(double StartSeconds, double EndSeconds);

    private record EpisodeData(
        SerieEpisode Episode,
        IndexedFile File,
        double DurationSeconds,
        uint[]? IntroFingerprint,
        uint[]? OutroFingerprint);
}
