using K7.Server.Application.Common;
using K7.Server.Application.Features.IndexedFiles.Commands.BackfillVideoFrameRate;
using K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFilePlaybackTracks;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;
using K7.Server.Domain.Common;
using K7.Shared.Dtos;

namespace K7.Server.Web.Endpoints.StreamSessions;

/// <summary>
/// Creates a stream session and negotiates its first stream (direct play or HLS).
/// Shared by the regular endpoint and the public share endpoint.
/// </summary>
public static class StreamSessionNegotiator
{
    public static async Task<StreamingSessionDto> CreateAsync(
        ISender sender,
        CreateStreamSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await sender.Send(command, cancellationToken);

        // Negotiate the initial stream (direct-play vs HLS)
        var query = new GetStreamUriQuery
        {
            Id = session.IndexedFileId,
            DeviceId = command.DeviceId,
            StreamSessionId = session.Id,
            AudioTrackIndex = command.AudioTrackIndex,
            SubtitleTrackIndex = command.SubtitleTrackIndex,
            AllowAudioPassthrough = command.AudioPassthrough,
            StartSeconds = command.StartSeconds
        };

        var streamUri = await sender.Send(query, cancellationToken);

        var playbackTracks = await sender.Send(
            new GetIndexedFilePlaybackTracksQuery(command.IndexedFileId),
            cancellationToken);

        session.Source = streamUri;
        session.PlaybackSettings.AudioTrackIndex = query.AudioTrackIndex ?? 0;
        session.PlaybackSettings.SubtitleTrackIndex = query.SubtitleTrackIndex;
        session.AudioTracks = playbackTracks.AudioTracks;
        session.SubtitleTracks = playbackTracks.SubtitleTracks;
        session.StreamDecision = streamUri.StreamDecision;
        var videoTracks = playbackTracks.VideoTracks;
        if (videoTracks.Count > 0 && videoTracks.All(t => VideoFrameRate.IsMissing(t.FrameRate)))
        {
            var backfilled = await sender.Send(
                new BackfillVideoFrameRateCommand(command.IndexedFileId),
                cancellationToken);
            if (backfilled is { Count: > 0 })
                videoTracks = backfilled;
        }

        StreamSessionVideoTiming.CopyFrom(session, videoTracks);

        return session;
    }
}
