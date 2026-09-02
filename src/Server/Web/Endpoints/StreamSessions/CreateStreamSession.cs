using K7.Server.Application.Common;
using K7.Server.Application.Features.IndexedFiles.Commands.BackfillVideoFrameRate;
using K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFilePlaybackTracks;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;
using K7.Server.Domain.Common;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class CreateStreamSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/stream-sessions", async (
            [FromServices] ISender sender,
            [FromBody] CreateStreamSessionCommand command,
            CancellationToken cancellationToken) =>
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
                AllowAudioPassthrough = command.AudioPassthrough
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

            return Results.Created($"/api/stream-sessions/{session.Id}", session);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
