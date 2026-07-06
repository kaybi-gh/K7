using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;

public record AnalyzeMusicTrackAudioCommand : IRequest
{
    public required Guid TrackId { get; init; }
}

public class AnalyzeMusicTrackAudioCommandHandler(
    IApplicationDbContext context,
    IWaveformGenerator waveformGenerator,
    IFadeAnalyzer fadeAnalyzer,
    IAudioTagReader audioTagReader,
    ILoudnessAnalyzer loudnessAnalyzer,
    ILogger<AnalyzeMusicTrackAudioCommandHandler> logger) : IRequestHandler<AnalyzeMusicTrackAudioCommand>
{
    public async Task Handle(AnalyzeMusicTrackAudioCommand request, CancellationToken cancellationToken)
    {
        var track = await context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.IndexedFiles)
            .Include(t => t.AudioAnalysis)
            .FirstOrDefaultAsync(t => t.Id == request.TrackId, cancellationToken);

        if (track is null)
        {
            logger.LogWarning("Track {TrackId} not found for audio analysis", request.TrackId);
            return;
        }

        if (track.AudioAnalysis is not null)
        {
            logger.LogDebug("Track '{Title}' already has audio analysis, skipping", track.Title);
            return;
        }

        var filePath = track.IndexedFiles.FirstOrDefault()?.Path;
        if (filePath is null)
        {
            logger.LogWarning("Track '{Title}' has no indexed file, skipping analysis", track.Title);
            return;
        }

        // Waveform peaks via ffmpeg (always available)
        var waveformPeaks = await waveformGenerator.GenerateAsync(filePath, cancellationToken: cancellationToken);

        // MixRamp fade detection via ffmpeg silencedetect
        var fadeResult = await fadeAnalyzer.AnalyzeAsync(filePath, cancellationToken);

        // ReplayGain from file tags
        var tags = audioTagReader.ReadTags(filePath, includeCoverArt: false);

        // Loudness via ffmpeg ebur128 filter
        var loudnessLufs = await loudnessAnalyzer.AnalyzeLufsAsync(filePath, cancellationToken);

        if (waveformPeaks is null && fadeResult is null && tags?.ReplayGainTrackGain is null && loudnessLufs is null)
        {
            logger.LogWarning(
                "Audio analysis produced no results for track '{Title}' ({TrackId}), will retry",
                track.Title,
                track.Id);
            throw new InvalidOperationException($"Audio analysis produced no results for track {track.Id}.");
        }

        var analysis = new AudioAnalysis
        {
            AnalyzedAt = DateTime.UtcNow,
            WaveformPeaks = waveformPeaks,
            MusicTrackId = track.Id,
            LoudnessLufs = loudnessLufs
        };

        if (fadeResult is not null)
        {
            analysis.FadeInDuration = fadeResult.FadeInDuration;
            analysis.FadeOutDuration = fadeResult.FadeOutDuration;
        }

        if (tags is not null)
        {
            analysis.ReplayGainTrackGain = tags.ReplayGainTrackGain;
            analysis.ReplayGainAlbumGain = tags.ReplayGainAlbumGain;
        }

        context.AudioAnalysis.Add(analysis);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Audio analysis completed for '{Title}' (LUFS={Lufs}, FadeOut={FadeOut}s)",
            track.Title, analysis.LoudnessLufs, analysis.FadeOutDuration);
    }
}
