using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class CreateFederationStreamSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/stream-sessions", async (
            [FromBody] CreateFederationStreamSessionRequest request,
            [FromServices] IApplicationDbContext context,
            [FromServices] ISender sender,
            [FromServices] IActiveStreamTracker activeStreamTracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            var sharedLibraryIds = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound && a.IsEnabled)
                .Select(a => a.LibraryId)
                .ToListAsync(cancellationToken);

            var indexedFile = await context.IndexedFiles
                .Include(f => f.FileMetadata)
                .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId && sharedLibraryIds.Contains(f.LibraryId), cancellationToken);

            if (indexedFile is null)
                return Results.NotFound();

            // Check concurrent stream quota
            var agreement = await context.PeerShareAgreements
                .FirstOrDefaultAsync(a => a.PeerServerId == peer.Id
                    && a.LibraryId == indexedFile.LibraryId
                    && a.Direction == ShareDirection.Outbound, cancellationToken);

            if (agreement?.MaxConcurrentStreams is not null)
            {
                var activeStreams = await context.StreamSessions
                    .CountAsync(s => s.PeerServerId == peer.Id && s.EndedAt == null, cancellationToken);

                if (activeStreams >= agreement.MaxConcurrentStreams)
                    return Results.StatusCode(429);
            }

            // Build a virtual device from the peer's capabilities
            var deviceCapabilities = BuildPlaybackCapabilities(request.DeviceCapabilities);
            var virtualDevice = new Device
            {
                Id = Guid.NewGuid(),
                ClientType = ClientType.Web,
                PlaybackCapabilities = deviceCapabilities
            };

            // Create session
            var session = new StreamSession
            {
                Id = Guid.NewGuid(),
                IndexedFileId = indexedFile.Id,
                PeerServerId = peer.Id,
                State = PlaybackState.Idle,
                Position = 0,
                PlaybackSettingsJson = "{}"
            };

            context.StreamSessions.Add(session);
            await context.SaveChangesAsync(cancellationToken);

            // Decide stream URI
            IndexedFileStreamUri streamUri;
            StreamDecisionDto? streamDecision = null;
            double duration = 0;

            if (indexedFile.FileMetadata is AudioFileMetadata audioMeta)
            {
                await context.Entry(audioMeta).Reference(a => a.AudioTrack).LoadAsync(cancellationToken);

                var query = new GetStreamUriQuery
                {
                    Id = indexedFile.Id,
                    DeviceId = virtualDevice.Id,
                    StreamSessionId = session.Id,
                    AudioTrackIndex = request.AudioTrackIndex
                };

                var (uri, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(virtualDevice, indexedFile, audioMeta, query);
                streamUri = uri;
                streamDecision = decision;
                duration = audioMeta.Duration.TotalSeconds;
            }
            else if (indexedFile.FileMetadata is VideoFileMetadata videoMeta)
            {
                await context.Entry(videoMeta).Collection(v => v.AudioTracks).LoadAsync(cancellationToken);
                await context.Entry(videoMeta).Collection(v => v.VideoTracks).LoadAsync(cancellationToken);
                await context.Entry(videoMeta).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

                var hlsSegmentsAvailable = await context.HlsSegments
                    .AnyAsync(s => s.IndexedFileId == indexedFile.Id, cancellationToken);

                if (!hlsSegmentsAvailable)
                {
                    await sender.Send(new CreateBackgroundTaskCommand
                    {
                        Request = new ComputeHlsSegmentsCommand
                        {
                            Id = indexedFile.Id,
                            SegmentsDuration = TimeSpan.FromSeconds(2)
                        },
                        Priority = BackgroundTaskPriority.High,
                        TargetEntityId = indexedFile.Id,
                        TargetEntityTypeName = nameof(IndexedFile),
                        MaxAttempts = 5,
                        ConcurrencyGroup = "ffmpeg"
                    }, cancellationToken);
                }

                var query = new GetStreamUriQuery
                {
                    Id = indexedFile.Id,
                    DeviceId = virtualDevice.Id,
                    StreamSessionId = session.Id,
                    AudioTrackIndex = request.AudioTrackIndex
                };

                var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
                    virtualDevice, indexedFile, videoMeta, query, hlsSegmentsAvailable, subtitleTrackIndex: null);
                streamUri = uri;
                streamDecision = decision;
                duration = videoMeta.Duration.TotalSeconds;
            }
            else
            {
                return Results.UnprocessableEntity();
            }

            // Track as active stream for admin visibility
            var media = await context.Medias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IndexedFiles.Any(f => f.Id == indexedFile.Id), cancellationToken);

            activeStreamTracker.Upsert(session.Id, new ActiveStreamInfo
            {
                SessionId = session.Id,
                IdentityUserId = clientId,
                UserName = peer.Name,
                MediaId = media?.Id,
                MediaTitle = media?.Title,
                MediaType = media?.GetType().Name,
                DeviceName = peer.Name,
                DeviceType = "Federation",
                StreamDecision = streamDecision,
                Duration = duration,
                StartedAt = DateTime.UtcNow
            });

            var result = new StreamingSessionDto
            {
                Id = session.Id,
                IndexedFileId = session.IndexedFileId!.Value,
                State = session.State,
                Position = session.Position,
                PlaybackSettings = new PlaybackSettingsDto(),
                Source = streamUri
            };

            return Results.Created($"/api/federation/stream-sessions/{session.Id}", result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static DevicePlaybackCapabilities BuildPlaybackCapabilities(DevicePlaybackCapabilitiesDto dto)
    {
        var formatIds = dto.SupportedMediaFormats.Select(f => f.Id).ToList();
        return new DevicePlaybackCapabilities
        {
            SupportedMediaFormatIds = formatIds,
            SupportedSubtitlesCodecs = [.. dto.SupportedSubtitlesCodecs],
            SupportsHDR = dto.SupportsHDR
        };
    }
}
