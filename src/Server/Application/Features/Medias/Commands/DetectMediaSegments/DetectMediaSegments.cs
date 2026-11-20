using System.Numerics;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
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

    // Segment duration limits (seconds) for audio matches. Named chapters and picture-based
    // credits have their own in ChapterSegmentFallbackHelper and CreditsFrameAnalyzer.
    private const double MinIntroDurationSeconds = 15;

    // A shorter intro (title card) is only kept when several episodes share the same region.
    private const double ShortIntroMinDurationSeconds = 8;
    // Above the two minutes of a long opening: some shows open every episode on the same
    // logos and disclaimer before it, and the whole shared stretch is the intro.
    private const double MaxIntroDurationSeconds = 180;
    private const double MinOutroDurationSeconds = 15;
    private const double MaxOutroDurationSeconds = 450;

    // Analysis window
    private const double AnalysisLengthLimitSeconds = 600;
    private const double AnalysisPercent = 0.25;

    // Credits are searched in the last 450s even when that is more than AnalysisPercent, so an
    // ending followed by a long preview or post-credits scene is still inside the window.
    private const double OutroWindowSeconds = 450;
    private const double OutroWindowMaxShare = 0.5;

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

        var serieLocked = await _context.Medias
            .OfType<Serie>()
            .AsNoTracking()
            .Where(s => s.Id == season.SerieId)
            .Select(s => s.LockedFields)
            .FirstOrDefaultAsync(cancellationToken);

        if (serieLocked is not null && serieLocked.Contains(MediaLockFields.IntroOutro))
        {
            _logger.LogInformation(
                "Skipping segment detection for season {SeasonId}: serie {SerieId} has {Lock} locked",
                request.SeasonId,
                season.SerieId,
                MediaLockFields.IntroOutro);
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

        var presentTypes = episodes.ToDictionary(
            e => e.Id,
            e => e.Segments
                .Where(s => IntroOutroSegmentCompleteness.CountsAsPresent(s.Type, s.StartMs, s.EndMs))
                .Select(s => s.Type)
                .ToHashSet());

        // Episodes whose audio was fingerprinted, then those whose picture could not be read:
        // only an episode analysed to the end is settled as "nothing more to find".
        var fingerprinted = new HashSet<Guid>();
        var pictureFailed = new HashSet<Guid>();

        var newSegments = await RunFingerprintDetectionAsync(episodes, presentTypes, fingerprinted, cancellationToken);
        MarkPresent(presentTypes, newSegments);

        var chapterFallbacks = await BuildChapterFallbackSegmentsAsync(episodes, presentTypes, cancellationToken);
        MarkPresent(presentTypes, chapterFallbacks);
        newSegments.AddRange(chapterFallbacks);

        var pictureCredits = await BuildPictureCreditsSegmentsAsync(
            episodes, presentTypes, pictureFailed, cancellationToken);
        MarkPresent(presentTypes, pictureCredits);
        newSegments.AddRange(pictureCredits);

        if (newSegments.Count > 0)
        {
            // Only missing types are detected, but replace rather than add in case a concurrent
            // run wrote the same segment first.
            foreach (var group in newSegments.GroupBy(s => s.Type))
            {
                var type = group.Key;
                var episodeIds = group.Select(s => s.MediaId).Distinct().ToList();
                await _context.MediaSegments
                    .Where(s => s.Type == type && episodeIds.Contains(s.MediaId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            _context.MediaSegments.AddRange(newSegments);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var settledIds = fingerprinted.Where(id => !pictureFailed.Contains(id)).ToList();
        if (settledIds.Count > 0)
        {
            await _context.Medias
                .OfType<SerieEpisode>()
                .Where(e => settledIds.Contains(e.Id)
                    && (e.SegmentDetectionVersion == null
                        || e.SegmentDetectionVersion < IntroOutroSegmentCompleteness.CurrentDetectionVersion))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        e => e.SegmentDetectionVersion,
                        IntroOutroSegmentCompleteness.CurrentDetectionVersion),
                    cancellationToken);
        }

        await TryQueueThemeExtractAsync(season.SerieId, cancellationToken);
    }

    private static void MarkPresent(
        Dictionary<Guid, HashSet<MediaSegmentType>> presentTypes,
        IEnumerable<MediaSegment> segments)
    {
        foreach (var segment in segments)
            presentTypes[segment.MediaId].Add(segment.Type);
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
            TargetEntityId = serieId,
            TargetEntityTypeName = nameof(Serie),
            Lane = BackgroundTaskLane.MediaAnalysis,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 2
        }, cancellationToken);
    }

    private async Task<List<MediaSegment>> RunFingerprintDetectionAsync(
        List<SerieEpisode> episodes,
        Dictionary<Guid, HashSet<MediaSegmentType>> presentTypes,
        HashSet<Guid> fingerprinted,
        CancellationToken cancellationToken)
    {
        var segments = new List<MediaSegment>();
        if (!episodes.Any(e => IntroOutroSegmentCompleteness.IsIncomplete(presentTypes[e.Id])))
            return segments;

        // Fingerprints for the whole season: an episode missing a segment is matched against
        // every other one, including those that are already complete.
        var episodeData = await ExtractAllFingerprintsAsync(episodes, cancellationToken);

        if (episodeData.Count < 2)
        {
            _logger.LogWarning("Only {Count} episodes with valid fingerprints, need at least 2", episodeData.Count);
            return segments;
        }

        LogFingerprintDiagnostics(episodeData);

        foreach (var data in episodeData.Where(d => d.IntroFingerprint is not null && d.OutroFingerprint is not null))
            fingerprinted.Add(data.Episode.Id);

        var introTargets = TargetsMissing(episodeData, presentTypes, MediaSegmentType.Intro);
        var outroTargets = TargetsMissing(episodeData, presentTypes, MediaSegmentType.Outro);

        var introMatches = ChromaprintFingerprintMatcher.FindBestMatches(
            episodeData.Select(e => e.IntroFingerprint).ToList(),
            MinIntroDurationSeconds,
            MaxIntroDurationSeconds,
            ShortIntroMinDurationSeconds,
            introTargets,
            cancellationToken);
        var outroMatches = ChromaprintFingerprintMatcher.FindBestMatches(
            episodeData.Select(e => e.OutroFingerprint).ToList(),
            MinOutroDurationSeconds,
            MaxOutroDurationSeconds,
            shortMinDurationSeconds: null,
            outroTargets,
            cancellationToken);

        _logger.LogInformation(
            "Found intros for {IntroCount}/{IntroTargets} and outros for {OutroCount}/{OutroTargets} episodes missing them ({Total} fingerprinted)",
            introMatches.Count, introTargets.Count, outroMatches.Count, outroTargets.Count, episodeData.Count);

        for (var i = 0; i < episodeData.Count; i++)
        {
            var data = episodeData[i];

            // A match as long as the window means the two files are the same recording.
            if (introMatches.TryGetValue(i, out var introMatch)
                && introMatch.EndSeconds - introMatch.StartSeconds < data.IntroWindowSeconds - 1)
            {
                var (start, end) = await AdjustSegmentBoundariesAsync(
                    data.File.Path, introMatch.StartSeconds, introMatch.EndSeconds,
                    data.DurationSeconds, cancellationToken);

                // Snapping the end to a silence can eat half of a title card of a few seconds.
                if (end - start < ShortIntroMinDurationSeconds)
                    end = introMatch.EndSeconds;

                segments.Add(CreateSegment(data.Episode.Id, MediaSegmentType.Intro, start, end));
            }

            if (outroMatches.TryGetValue(i, out var outroMatch)
                && outroMatch.EndSeconds - outroMatch.StartSeconds < data.OutroWindowSeconds - 1)
            {
                var outroOffset = data.DurationSeconds - data.OutroWindowSeconds;
                var absoluteStart = outroOffset + outroMatch.StartSeconds;
                var absoluteEnd = outroOffset + outroMatch.EndSeconds;

                // The wider credits window can reach the middle of a short episode: a jingle
                // repeated there is not an ending unless it runs into the last quarter.
                if (absoluteEnd < data.DurationSeconds - GetAnalysisWindowSeconds(data.DurationSeconds))
                    continue;

                var (start, end) = await AdjustSegmentBoundariesAsync(
                    data.File.Path, absoluteStart, absoluteEnd,
                    data.DurationSeconds, cancellationToken);

                segments.Add(CreateSegment(data.Episode.Id, MediaSegmentType.Outro, start, end));
            }
        }

        return segments;
    }

    private static HashSet<int> TargetsMissing(
        List<EpisodeData> episodeData,
        Dictionary<Guid, HashSet<MediaSegmentType>> presentTypes,
        MediaSegmentType type)
    {
        var targets = new HashSet<int>();
        for (var i = 0; i < episodeData.Count; i++)
        {
            if (IntroOutroSegmentCompleteness.NeedsType(presentTypes[episodeData[i].Episode.Id], type))
                targets.Add(i);
        }

        return targets;
    }

    private static MediaSegment CreateSegment(Guid episodeId, MediaSegmentType type, double startSeconds, double endSeconds)
    {
        return new MediaSegment
        {
            Id = Guid.NewGuid(),
            MediaId = episodeId,
            Type = type,
            StartMs = (long)(startSeconds * 1000),
            EndMs = (long)(endSeconds * 1000),
            DetectedAt = DateTimeOffset.UtcNow
        };
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

                var matchingAtThreshold = popCounts
                    .Take(ChromaprintFingerprintMatcher.MaxFingerprintPointDifferences + 1)
                    .Sum();
                _logger.LogInformation(
                    "Diagnostic ep1 vs ep2 at shift=0: overlap={Overlap}, matching (popcount<={Threshold}): {Matching}/{Total} ({Pct:F1}%), popcount[0]={P0}, [1-3]={P13}, [4-6]={P46}, [7-10]={P710}, [11+]={P11}",
                    overlapLen, ChromaprintFingerprintMatcher.MaxFingerprintPointDifferences,
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

            var introWindow = GetAnalysisWindowSeconds(durationSeconds);
            var outroWindow = GetOutroWindowSeconds(durationSeconds);

            var introFpBytes = await _chromaprintService.ExtractFingerprintAsync(
                file.Path, TimeSpan.Zero, TimeSpan.FromSeconds(introWindow), cancellationToken);

            var outroStart = durationSeconds - outroWindow;
            var outroFpBytes = await _chromaprintService.ExtractFingerprintAsync(
                file.Path, TimeSpan.FromSeconds(outroStart), TimeSpan.FromSeconds(outroWindow), cancellationToken);

            var introFp = introFpBytes is not null ? ConvertToUints(introFpBytes) : null;
            var outroFp = outroFpBytes is not null ? ConvertToUints(outroFpBytes) : null;

            if (introFp is null && outroFp is null)
                continue;

            result.Add(new EpisodeData(episode, file, durationSeconds, introWindow, outroWindow, introFp, outroFp));
        }

        return result;
    }

    // -- Chapter-title fallback (when chromaprint misses Intro/Outro) --

    private async Task<List<MediaSegment>> BuildChapterFallbackSegmentsAsync(
        List<SerieEpisode> episodes,
        Dictionary<Guid, HashSet<MediaSegmentType>> presentTypes,
        CancellationToken cancellationToken)
    {
        var segments = new List<MediaSegment>();

        foreach (var episode in episodes)
        {
            var file = episode.IndexedFiles.FirstOrDefault(f => f.FileMetadata is not null && File.Exists(f.Path));
            if (file is null)
                continue;

            var durationSeconds = GetDurationSeconds(file);
            if (durationSeconds <= 0)
                continue;

            var existing = presentTypes[episode.Id];
            var needIntro = !existing.Contains(MediaSegmentType.Intro);
            var needOutro = !existing.Contains(MediaSegmentType.Outro);
            if (!needIntro && !needOutro)
                continue;

            var chapters = await _segmentDetectionService.GetChaptersAsync(file.Path, cancellationToken);
            if (chapters.Count == 0)
                continue;

            if (needIntro)
            {
                foreach (var chapter in chapters)
                {
                    if (!ChapterSegmentFallbackHelper.TryCreateSegment(
                            chapter, durationSeconds, MediaSegmentType.Intro, out var start, out var end))
                        continue;

                    segments.Add(new MediaSegment
                    {
                        Id = Guid.NewGuid(),
                        MediaId = episode.Id,
                        Type = MediaSegmentType.Intro,
                        StartMs = (long)(start * 1000),
                        EndMs = (long)(end * 1000),
                        DetectedAt = DateTimeOffset.UtcNow
                    });
                    _logger.LogInformation(
                        "Chapter fallback Intro for episode {EpisodeId}: {Start:F1}s-{End:F1}s ({Title})",
                        episode.Id, start, end, chapter.Title);
                    break;
                }
            }

            if (needOutro)
            {
                foreach (var chapter in chapters.Reverse())
                {
                    if (!ChapterSegmentFallbackHelper.TryCreateSegment(
                            chapter, durationSeconds, MediaSegmentType.Outro, out var start, out var end))
                        continue;

                    segments.Add(new MediaSegment
                    {
                        Id = Guid.NewGuid(),
                        MediaId = episode.Id,
                        Type = MediaSegmentType.Outro,
                        StartMs = (long)(start * 1000),
                        EndMs = (long)(end * 1000),
                        DetectedAt = DateTimeOffset.UtcNow
                    });
                    _logger.LogInformation(
                        "Chapter fallback Outro for episode {EpisodeId}: {Start:F1}s-{End:F1}s ({Title})",
                        episode.Id, start, end, chapter.Title);
                    break;
                }
            }
        }

        return segments;
    }

    // -- Picture-based credits (when neither audio nor chapters found an Outro) --

    private async Task<List<MediaSegment>> BuildPictureCreditsSegmentsAsync(
        List<SerieEpisode> episodes,
        Dictionary<Guid, HashSet<MediaSegmentType>> presentTypes,
        HashSet<Guid> pictureFailed,
        CancellationToken cancellationToken)
    {
        var segments = new List<MediaSegment>();

        foreach (var episode in episodes)
        {
            if (presentTypes[episode.Id].Contains(MediaSegmentType.Outro))
                continue;

            // The picture of a file does not change: one scan per detection version is enough,
            // unlike audio matching which gains partners as episodes are added.
            if (IntroOutroSegmentCompleteness.IsSettled(episode.SegmentDetectionVersion))
                continue;

            var file = episode.IndexedFiles.FirstOrDefault(f => f.FileMetadata is VideoFileMetadata && File.Exists(f.Path));
            if (file is null)
                continue;

            var durationSeconds = GetDurationSeconds(file);
            if (durationSeconds <= 0)
                continue;

            var tailSeconds = CreditsFrameAnalyzer.GetTailSeconds(durationSeconds);
            var tailStart = durationSeconds - tailSeconds;

            var frames = await _segmentDetectionService.SampleCreditsFramesAsync(
                file.Path, tailStart, tailSeconds, cancellationToken);
            if (frames is null)
            {
                pictureFailed.Add(episode.Id);
                continue;
            }

            var run = CreditsFrameAnalyzer.FindCreditsRun(frames, tailStart, durationSeconds);
            if (run is null)
                continue;

            segments.Add(CreateSegment(episode.Id, MediaSegmentType.Outro, run.Value.StartSeconds, run.Value.EndSeconds));
            _logger.LogInformation(
                "Picture-based Outro for episode {EpisodeId}: {Start:F1}s-{End:F1}s",
                episode.Id, run.Value.StartSeconds, run.Value.EndSeconds);
        }

        return segments;
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

    private static double GetOutroWindowSeconds(double totalDurationSeconds)
    {
        return Math.Max(
            GetAnalysisWindowSeconds(totalDurationSeconds),
            Math.Min(OutroWindowSeconds, totalDurationSeconds * OutroWindowMaxShare));
    }

    // -- Inner types --

    private record EpisodeData(
        SerieEpisode Episode,
        IndexedFile File,
        double DurationSeconds,
        double IntroWindowSeconds,
        double OutroWindowSeconds,
        uint[]? IntroFingerprint,
        uint[]? OutroFingerprint);
}
