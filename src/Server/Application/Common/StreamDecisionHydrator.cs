using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Common;

public static class StreamDecisionHydrator
{
    public static async Task TryHydrateTrackerAsync(
        Guid sessionId,
        IActiveStreamTracker tracker,
        IApplicationDbContext context,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var existing = tracker.GetStreamInfo(sessionId)?.StreamDecision;
        if (existing.HasTrackDetails())
            return;

        StreamDecisionDto? rebuilt = null;
        try
        {
            rebuilt = await TryFromStreamSessionAsync(
                sessionId,
                tracker,
                context,
                ffmpegCapabilitiesService,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to rebuild stream decision from stream session {SessionId}", sessionId);
        }

        if (rebuilt.HasTrackDetails())
        {
            tracker.UpdateStreamDecision(sessionId, StreamDecisionMerge.Merge(existing, rebuilt!));
            return;
        }

        var fromDetails = await TryFromPlaybackSessionDetailsAsync(sessionId, context, cancellationToken);
        if (!fromDetails.HasTrackDetails())
            return;

        tracker.UpdateStreamDecision(sessionId, StreamDecisionMerge.Merge(existing, fromDetails!));
        await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(
            sessionId,
            tracker,
            ffmpegCapabilitiesService,
            cancellationToken);
    }

    private static async Task<StreamDecisionDto?> TryFromPlaybackSessionDetailsAsync(
        Guid sessionId,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var details = await context.PlaybackSessionDetails
            .AsNoTracking()
            .Where(d => d.MediaPlaybackSession.SessionId == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

        return details.ToStreamDecisionDto();
    }

    private static async Task<StreamDecisionDto?> TryFromStreamSessionAsync(
        Guid sessionId,
        IActiveStreamTracker tracker,
        IApplicationDbContext context,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        CancellationToken cancellationToken)
    {
        var streamSession = await ResolveStreamSessionAsync(sessionId, tracker, context, cancellationToken);
        if (streamSession?.IndexedFileId is not { } indexedFileId
            || streamSession.DeviceId is not { } deviceId)
        {
            return null;
        }

        var device = await context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
            return null;

        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == indexedFileId, cancellationToken);

        if (indexedFile?.FileMetadata is null)
            return null;

        var playbackSettings = DeserializePlaybackSettings(streamSession.PlaybackSettingsJson);

        StreamDecisionDto decision;

        switch (indexedFile.FileMetadata)
        {
            case AudioFileMetadata audioMeta:
                await context.Entry(audioMeta).Reference(a => a.AudioTrack).LoadAsync(cancellationToken);

                var audioQuery = new GetStreamUriQuery
                {
                    Id = indexedFileId,
                    DeviceId = deviceId,
                    StreamSessionId = sessionId
                };

                (_, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(device, indexedFile, audioMeta, audioQuery);
                break;

            case VideoFileMetadata videoMeta:
                await context.Entry(videoMeta).Collection(v => v.AudioTracks).LoadAsync(cancellationToken);
                await context.Entry(videoMeta).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
                await context.Entry(videoMeta).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

                var audioTrackIndex = ResolveAudioTrackIndex(videoMeta.AudioTracks, playbackSettings.AudioTrackIndex);
                var subtitleTrackIndex = ResolveSubtitleTrackIndex(videoMeta.SubtitleTracks, playbackSettings.SubtitleTrackIndex);

                var videoQuery = new GetStreamUriQuery
                {
                    Id = indexedFileId,
                    DeviceId = deviceId,
                    StreamSessionId = sessionId,
                    AudioTrackIndex = audioTrackIndex,
                    SubtitleTrackIndex = subtitleTrackIndex
                };

                var hlsSegmentsAvailable = await HlsSegmentHelper.HasSegmentsAsync(context, indexedFileId, cancellationToken);
                (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
                    device,
                    indexedFile,
                    videoMeta,
                    videoQuery,
                    hlsSegmentsAvailable,
                    subtitleTrackIndex);
                break;

            default:
                return null;
        }

        return await StreamDecisionEnrichment.EnrichEncodersAsync(decision, ffmpegCapabilitiesService, cancellationToken);
    }

    private static async Task<StreamSession?> ResolveStreamSessionAsync(
        Guid sessionId,
        IActiveStreamTracker tracker,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var streamSession = await context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (streamSession is not null)
            return streamSession;

        var info = tracker.GetStreamInfo(sessionId);
        if (info?.DeviceId is not { } deviceId || info.MediaId is not { } mediaId)
            return null;

        var indexedFileIds = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.MediaId == mediaId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        if (indexedFileIds.Count == 0)
            return null;

        return await context.StreamSessions
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId
                && s.IndexedFileId != null
                && indexedFileIds.Contains(s.IndexedFileId.Value))
            .OrderByDescending(s => s.LastModified)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static int? ResolveAudioTrackIndex(IEnumerable<AudioFileTrack> tracks, int configuredIndex)
    {
        var trackList = tracks.ToList();
        if (trackList.Any(t => t.Index == configuredIndex))
            return configuredIndex;

        return null;
    }

    private static int? ResolveSubtitleTrackIndex(IEnumerable<SubtitleFileTrack> tracks, int? configuredIndex)
    {
        if (configuredIndex is not int index)
            return null;

        return tracks.Any(t => t.Index == index) ? index : null;
    }

    private static PlaybackSettingsDto DeserializePlaybackSettings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PlaybackSettingsDto>(json) ?? new PlaybackSettingsDto();
        }
        catch (JsonException)
        {
            return new PlaybackSettingsDto();
        }
    }
}
