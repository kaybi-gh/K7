using System.Numerics;
using K7.Server.Application.Common.Interfaces;
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
    private readonly ILogger<DetectMediaSegmentsCommandHandler> _logger;

    private const int IntroSearchDurationMinutes = 10;
    private const double OutroSearchPercent = 0.25;
    private const int MinSegmentDurationMs = 20_000;

    public DetectMediaSegmentsCommandHandler(
        IApplicationDbContext context,
        IChromaprintService chromaprintService,
        ILogger<DetectMediaSegmentsCommandHandler> logger)
    {
        _context = context;
        _chromaprintService = chromaprintService;
        _logger = logger;
    }

    public async Task Handle(DetectMediaSegmentsCommand request, CancellationToken cancellationToken)
    {
        var season = await _context.Medias
            .OfType<SerieSeason>()
            .Include(s => s.Episodes)
                .ThenInclude(e => e.IndexedFiles)
                    .ThenInclude(f => f.FileMetadata)
            .Include(s => s.Episodes)
                .ThenInclude(e => e.Segments)
            .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);

        if (season is null)
        {
            _logger.LogWarning("Season {SeasonId} not found for media segment detection", request.SeasonId);
            return;
        }

        var episodes = season.Episodes
            .Where(e => e.IndexedFiles.Any(f => f.FileMetadata is not null && File.Exists(f.Path)))
            .OrderBy(e => e.EpisodeNumber)
            .ToList();

        if (episodes.Count < 2)
        {
            _logger.LogDebug("Season {SeasonId} has fewer than 2 episodes with files, skipping detection", request.SeasonId);
            return;
        }

        var hasExistingSegments = episodes.Any(e => e.Segments.Count > 0);

        if (hasExistingSegments)
        {
            await RunIncrementalDetectionAsync(episodes, cancellationToken);
        }
        else
        {
            await RunFullDetectionAsync(episodes, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task RunIncrementalDetectionAsync(List<SerieEpisode> episodes, CancellationToken cancellationToken)
    {
        var episodesWithSegments = episodes.Where(e => e.Segments.Count > 0).ToList();
        var episodesWithoutSegments = episodes.Where(e => e.Segments.Count == 0).ToList();

        if (episodesWithoutSegments.Count == 0)
            return;

        var referenceEpisode = episodesWithSegments.First();
        var referenceFile = referenceEpisode.IndexedFiles.FirstOrDefault(f => f.ChromaprintFingerprint is not null);

        if (referenceFile is null)
        {
            _logger.LogDebug("No cached fingerprint found for reference episode, falling back to full detection");
            await RunFullDetectionAsync(episodes, cancellationToken);
            return;
        }

        foreach (var episode in episodesWithoutSegments)
        {
            var file = episode.IndexedFiles.FirstOrDefault(f => f.FileMetadata is not null && File.Exists(f.Path));
            if (file is null)
                continue;

            var duration = GetDurationMs(file);
            if (duration <= 0)
                continue;

            var introFingerprint = await ExtractAndCacheFingerprintAsync(
                file, TimeSpan.Zero, TimeSpan.FromMinutes(IntroSearchDurationMinutes), cancellationToken);

            if (introFingerprint is null)
                continue;

            var introSegment = referenceEpisode.Segments.FirstOrDefault(s => s.Type == MediaSegmentType.Intro);
            if (introSegment is not null)
            {
                var referenceIntroFp = await ExtractFingerprintForRangeAsync(
                    referenceFile.Path, TimeSpan.Zero, TimeSpan.FromMinutes(IntroSearchDurationMinutes), cancellationToken);

                var match = FindMatchingRange(referenceIntroFp, introFingerprint);
                if (match is not null && match.Value.durationMs >= MinSegmentDurationMs)
                {
                    episode.Segments.Add(new MediaSegment
                    {
                        Id = Guid.NewGuid(),
                        MediaId = episode.Id,
                        Type = MediaSegmentType.Intro,
                        StartMs = match.Value.startMs,
                        EndMs = match.Value.startMs + match.Value.durationMs,
                        DetectedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            var outroSegment = referenceEpisode.Segments.FirstOrDefault(s => s.Type == MediaSegmentType.Outro);
            if (outroSegment is not null)
            {
                var outroStart = TimeSpan.FromMilliseconds(duration * (1.0 - OutroSearchPercent));
                var outroDuration = TimeSpan.FromMilliseconds(duration * OutroSearchPercent);
                var outroFingerprint = await ExtractFingerprintForRangeAsync(
                    file.Path, outroStart, outroDuration, cancellationToken);

                var referenceOutroStart = TimeSpan.FromMilliseconds(GetDurationMs(referenceFile) * (1.0 - OutroSearchPercent));
                var referenceOutroDuration = TimeSpan.FromMilliseconds(GetDurationMs(referenceFile) * OutroSearchPercent);
                var referenceOutroFp = await ExtractFingerprintForRangeAsync(
                    referenceFile.Path, referenceOutroStart, referenceOutroDuration, cancellationToken);

                var outroMatch = FindMatchingRange(referenceOutroFp, outroFingerprint);
                if (outroMatch is not null && outroMatch.Value.durationMs >= MinSegmentDurationMs)
                {
                    var absoluteStartMs = (long)(duration * (1.0 - OutroSearchPercent)) + outroMatch.Value.startMs;
                    episode.Segments.Add(new MediaSegment
                    {
                        Id = Guid.NewGuid(),
                        MediaId = episode.Id,
                        Type = MediaSegmentType.Outro,
                        StartMs = absoluteStartMs,
                        EndMs = absoluteStartMs + outroMatch.Value.durationMs,
                        DetectedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }
    }

    private async Task RunFullDetectionAsync(List<SerieEpisode> episodes, CancellationToken cancellationToken)
    {
        var introFingerprints = new List<(SerieEpisode episode, IndexedFile file, byte[] fingerprint, long durationMs)>();
        var outroFingerprints = new List<(SerieEpisode episode, IndexedFile file, byte[] fingerprint, long durationMs)>();

        foreach (var episode in episodes)
        {
            var file = episode.IndexedFiles.FirstOrDefault(f => f.FileMetadata is not null && File.Exists(f.Path));
            if (file is null)
                continue;

            var duration = GetDurationMs(file);
            if (duration <= 0)
                continue;

            var introFp = await ExtractAndCacheFingerprintAsync(
                file, TimeSpan.Zero, TimeSpan.FromMinutes(IntroSearchDurationMinutes), cancellationToken);

            if (introFp is not null)
                introFingerprints.Add((episode, file, introFp, duration));

            var outroStart = TimeSpan.FromMilliseconds(duration * (1.0 - OutroSearchPercent));
            var outroDuration = TimeSpan.FromMilliseconds(duration * OutroSearchPercent);
            var outroFp = await _chromaprintService.ExtractFingerprintAsync(
                file.Path, outroStart, outroDuration, cancellationToken);

            if (outroFp is not null)
                outroFingerprints.Add((episode, file, outroFp, duration));
        }

        if (introFingerprints.Count >= 2)
            DetectSegmentsFromFingerprints(introFingerprints, MediaSegmentType.Intro, isOutro: false);

        if (outroFingerprints.Count >= 2)
            DetectSegmentsFromFingerprints(outroFingerprints, MediaSegmentType.Outro, isOutro: true);
    }

    private void DetectSegmentsFromFingerprints(
        List<(SerieEpisode episode, IndexedFile file, byte[] fingerprint, long durationMs)> fingerprints,
        MediaSegmentType segmentType,
        bool isOutro)
    {
        // Compare first two episodes to establish the consensus range
        var (ep1, _, fp1, dur1) = fingerprints[0];
        var (ep2, _, fp2, dur2) = fingerprints[1];

        var match = FindMatchingRange(fp1, fp2);
        if (match is null || match.Value.durationMs < MinSegmentDurationMs)
        {
            _logger.LogDebug("No matching {SegmentType} range found between episodes", segmentType);
            return;
        }

        // Apply the detected range to all episodes
        foreach (var (episode, file, fp, durationMs) in fingerprints)
        {
            var startMs = match.Value.startMs;
            if (isOutro)
                startMs += (long)(durationMs * (1.0 - OutroSearchPercent));

            var endMs = startMs + match.Value.durationMs;

            // Skip if segment already exists
            if (episode.Segments.Any(s => s.Type == segmentType))
                continue;

            episode.Segments.Add(new MediaSegment
            {
                Id = Guid.NewGuid(),
                MediaId = episode.Id,
                Type = segmentType,
                StartMs = startMs,
                EndMs = endMs,
                DetectedAt = DateTimeOffset.UtcNow
            });
        }

        _logger.LogInformation("Detected {SegmentType} segment at {StartMs}-{EndMs}ms for {Count} episodes",
            segmentType, match.Value.startMs, match.Value.startMs + match.Value.durationMs, fingerprints.Count);
    }

    private async Task<byte[]?> ExtractAndCacheFingerprintAsync(
        IndexedFile file,
        TimeSpan startTime,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var fingerprint = await _chromaprintService.ExtractFingerprintAsync(
            file.Path, startTime, duration, cancellationToken);

        if (fingerprint is not null)
        {
            file.ChromaprintFingerprint = fingerprint;
            file.ChromaprintDurationSeconds = (int)duration.TotalSeconds;
            file.ChromaprintAnalyzedAt = DateTimeOffset.UtcNow;
        }

        return fingerprint;
    }

    private async Task<byte[]?> ExtractFingerprintForRangeAsync(
        string filePath,
        TimeSpan startTime,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        return await _chromaprintService.ExtractFingerprintAsync(filePath, startTime, duration, cancellationToken);
    }

    private static (long startMs, long durationMs)? FindMatchingRange(byte[]? fp1, byte[]? fp2)
    {
        if (fp1 is null || fp2 is null || fp1.Length < sizeof(int) || fp2.Length < sizeof(int))
            return null;

        var ints1 = ConvertToInts(fp1);
        var ints2 = ConvertToInts(fp2);

        if (ints1.Length == 0 || ints2.Length == 0)
            return null;

        // Each chromaprint integer covers approximately 0.1238 seconds (Chromaprint default)
        const double secondsPerItem = 0.1238;
        const int minMatchLength = 10;

        var bestStart = -1;
        var bestLength = 0;

        // Sliding window comparison: find the longest contiguous run of similar fingerprint values
        for (var offset = -(ints2.Length - 1); offset < ints1.Length; offset++)
        {
            var currentStart = -1;
            var currentLength = 0;

            var i1Start = Math.Max(0, offset);
            var i2Start = Math.Max(0, -offset);
            var len = Math.Min(ints1.Length - i1Start, ints2.Length - i2Start);

            for (var i = 0; i < len; i++)
            {
                var similarity = CountMatchingBits(ints1[i1Start + i], ints2[i2Start + i]);

                if (similarity >= 26) // 26 out of 32 bits matching = high similarity
                {
                    if (currentStart < 0)
                        currentStart = i1Start + i;
                    currentLength++;
                }
                else
                {
                    if (currentLength > bestLength)
                    {
                        bestStart = currentStart;
                        bestLength = currentLength;
                    }
                    currentStart = -1;
                    currentLength = 0;
                }
            }

            if (currentLength > bestLength)
            {
                bestStart = currentStart;
                bestLength = currentLength;
            }
        }

        if (bestLength < minMatchLength || bestStart < 0)
            return null;

        var startMs = (long)(bestStart * secondsPerItem * 1000);
        var durationMs = (long)(bestLength * secondsPerItem * 1000);

        return (startMs, durationMs);
    }

    private static int CountMatchingBits(int a, int b)
    {
        var xor = (uint)(a ^ b);
        return 32 - BitOperations.PopCount(xor);
    }

    private static int[] ConvertToInts(byte[] data)
    {
        var count = data.Length / sizeof(int);
        var result = new int[count];
        Buffer.BlockCopy(data, 0, result, 0, count * sizeof(int));
        return result;
    }

    private static long GetDurationMs(IndexedFile file)
    {
        return file.FileMetadata switch
        {
            VideoFileMetadata v => (long)v.Duration.TotalMilliseconds,
            AudioFileMetadata a => (long)a.Duration.TotalMilliseconds,
            _ => 0
        };
    }
}
