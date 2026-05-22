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
    IAudioAnalyzer audioAnalyzer,
    IWaveformGenerator waveformGenerator,
    IFadeAnalyzer fadeAnalyzer,
    IAudioTagReader audioTagReader,
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

        // Essentia analysis (optional - may not be installed)
        var analysis = await audioAnalyzer.AnalyzeAsync(filePath, cancellationToken);

        // Waveform peaks via ffmpeg (always available)
        var waveformPeaks = await waveformGenerator.GenerateAsync(filePath, cancellationToken: cancellationToken);

        // MixRamp fade detection via ffmpeg silencedetect
        var fadeResult = await fadeAnalyzer.AnalyzeAsync(filePath, cancellationToken);

        // ReplayGain from file tags
        var tags = audioTagReader.ReadTags(filePath, includeCoverArt: false);

        if (analysis is null && waveformPeaks is null && fadeResult is null && tags?.ReplayGainTrackGain is null)
            return;

        analysis ??= new AudioAnalysis { AnalyzedAt = DateTime.UtcNow };
        analysis.WaveformPeaks = waveformPeaks;
        analysis.MusicTrackId = track.Id;

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

        logger.LogInformation("Audio analysis completed for '{Title}' (BPM={Bpm}, Key={Key}, FadeOut={FadeOut}s)",
            track.Title, analysis.Bpm, analysis.MusicalKey, analysis.FadeOutDuration);
    }
}
