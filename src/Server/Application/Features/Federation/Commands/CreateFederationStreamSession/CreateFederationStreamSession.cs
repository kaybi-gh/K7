using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.CreateFederationStreamSession;

public record CreateFederationStreamSessionCommand(
    string? ClientId,
    CreateFederationStreamSessionRequest Request) : IRequest<CreateFederationStreamSessionResult>;

public record CreateFederationStreamSessionResult(StreamingSessionDto Session, string Location);

public class CreateFederationStreamSessionCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    ISender sender,
    IActiveStreamTracker activeStreamTracker)
    : IRequestHandler<CreateFederationStreamSessionCommand, CreateFederationStreamSessionResult>
{
    public async Task<CreateFederationStreamSessionResult> Handle(
        CreateFederationStreamSessionCommand command,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(command.ClientId, cancellationToken);
        var indexedFile = await peerAuthorization.RequireFileAccessibleToPeerAsync(
            peer.Id, command.Request.IndexedFileId, cancellationToken);

        await peerAuthorization.EnsureConcurrentStreamQuotaAsync(peer.Id, indexedFile.LibraryId, cancellationToken);

        var deviceCapabilities = BuildPlaybackCapabilities(command.Request.DeviceCapabilities);
        var virtualDevice = new Device
        {
            Id = Guid.NewGuid(),
            ClientType = ClientType.Web,
            PlaybackCapabilities = deviceCapabilities
        };

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
                AudioTrackIndex = command.Request.AudioTrackIndex
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
                AudioTrackIndex = command.Request.AudioTrackIndex
            };

            var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
                virtualDevice, indexedFile, videoMeta, query, hlsSegmentsAvailable, subtitleTrackIndex: null);
            streamUri = uri;
            streamDecision = decision;
            duration = videoMeta.Duration.TotalSeconds;
        }
        else
        {
            throw new UnprocessableEntityException("Unsupported file metadata type.");
        }

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IndexedFiles.Any(f => f.Id == indexedFile.Id), cancellationToken);

        activeStreamTracker.Upsert(session.Id, new ActiveStreamInfo
        {
            SessionId = session.Id,
            IdentityUserId = command.ClientId!,
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

        var playbackTracks = indexedFile.FileMetadata is VideoFileMetadata videoForTracks
            ? new
            {
                Audio = videoForTracks.AudioTracks.Select(t => t.ToAudioFileTrackDto()).ToList(),
                Subtitles = videoForTracks.SubtitleTracks.Select(t => t.ToSubtitleFileTrackDto()).ToList()
            }
            : null;

        var result = new StreamingSessionDto
        {
            Id = session.Id,
            IndexedFileId = session.IndexedFileId!.Value,
            State = session.State,
            Position = session.Position,
            PlaybackSettings = new PlaybackSettingsDto(),
            Source = streamUri,
            AudioTracks = playbackTracks?.Audio ?? [],
            SubtitleTracks = playbackTracks?.Subtitles ?? []
        };

        return new CreateFederationStreamSessionResult(result, $"/api/federation/stream-sessions/{session.Id}");
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
