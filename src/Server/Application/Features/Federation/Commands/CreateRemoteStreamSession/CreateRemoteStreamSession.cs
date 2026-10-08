using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.CreateRemoteStreamSession;

public record CreateRemoteStreamSessionCommand(
    CreateRemoteStreamSessionRequest Request,
    Guid UserId) : IRequest<CreateRemoteStreamSessionResult>;

public record CreateRemoteStreamSessionResult(StreamingSessionDto Session, string Location);

public class CreateRemoteStreamSessionCommandHandler(
    IApplicationDbContext context,
    IPeerAuthorizationService peerAuthorization,
    IPeerClient peerClient,
    IFederatedPlaybackSessionStore federatedSessionStore,
    IFederatedMediaCache federatedMediaCache,
    IActiveStreamTracker activeStreamTracker,
    ILogger<CreateRemoteStreamSessionCommandHandler> logger)
    : IRequestHandler<CreateRemoteStreamSessionCommand, CreateRemoteStreamSessionResult>
{
    public async Task<CreateRemoteStreamSessionResult> Handle(
        CreateRemoteStreamSessionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        var remoteFile = await context.RemoteIndexedFiles
            .Include(r => r.PeerServer)
            .FirstOrDefaultAsync(r => r.Id == request.RemoteFileId, cancellationToken);

        if (remoteFile?.PeerServer is null)
            throw new NotFoundException(request.RemoteFileId.ToString(), "RemoteIndexedFile");

        var peer = remoteFile.PeerServer;
        if (peer.Status != PeerStatus.Active)
            throw new PeerServerUnavailableException("Peer server is not active");

        if (peer.LastTestSucceeded == false)
            throw new PeerServerUnavailableException("Peer server is unreachable");

        var device = await context.Devices
            .FindAsync([request.DeviceId], cancellationToken);

        if (device is null)
            throw new NotFoundException(request.DeviceId.ToString(), nameof(Domain.Entities.Devices.Device));

        var auth = await peerAuthorization.AuthenticateOutboundAsync(peer.Id, cancellationToken);
        if (auth is null)
            throw new HttpRequestException("Failed to authenticate with peer.");

        var token = auth.Value.Token;
        var capabilitiesDto = device.PlaybackCapabilities.ToDevicePlaybackCapabilitiesDto();

        var federationRequest = new CreateFederationStreamSessionRequest
        {
            IndexedFileId = remoteFile.RemoteFileId,
            DeviceCapabilities = capabilitiesDto,
            AudioTrackIndex = request.AudioTrackIndex,
            SubtitleTrackIndex = request.SubtitleTrackIndex,
            ClientType = device.ClientType,
            OperatingSystem = device.OperatingSystem
        };

        var remoteSession = await peerClient.CreateRemoteStreamSessionAsync(
            peer.BaseUrl, token, federationRequest, cancellationToken);

        if (remoteSession is null)
            throw new HttpRequestException("Failed to create stream session on peer.");

        var assignedExecution = remoteSession.FederatedPlaybackExecution ?? FederatedPlaybackExecution.Origin;

        var localSession = new StreamSession
        {
            Id = Guid.NewGuid(),
            RemoteIndexedFileId = remoteFile.Id,
            DeviceId = device.Id,
            UserId = command.UserId,
            PeerServerId = peer.Id,
            RemoteSessionId = remoteSession.Id,
            FederatedPlaybackExecution = assignedExecution,
            State = PlaybackState.Idle,
            Position = 0,
            PlaybackSettingsJson = "{}"
        };

        context.StreamSessions.Add(localSession);
        await context.SaveChangesAsync(cancellationToken);

        var streamDecision = remoteSession.StreamDecision;
        IndexedFileStreamUri? localSource = null;

        // Peer HLS is remux and encode only. Direct Play stays a Range proxy of the
        // origin file so LibVLC can play the muxed container.
        if (streamDecision is not null && ShouldServePeerHls(assignedExecution, streamDecision))
        {
            var originDirectUrl =
                $"{peer.BaseUrl.TrimEnd('/')}/api/federation/stream-sessions/{remoteSession.Id}/direct-stream";

            var playbackState = new FederatedPlaybackSessionState
            {
                LocalSessionId = localSession.Id,
                RemoteSessionId = remoteSession.Id,
                PeerServerId = peer.Id,
                RemoteIndexedFileId = remoteFile.Id,
                OriginDirectStreamUrl = originDirectUrl,
                StreamDecision = streamDecision,
                Duration = remoteFile.Duration
            };
            federatedSessionStore.Set(playbackState);
            WarmFederatedMediaCache(playbackState);

            localSource = BuildLocalHlsSource(localSession.Id, streamDecision);
        }
        else if (remoteSession.Source is not null)
        {
            var remotePath = remoteSession.Source.Uri.IsAbsoluteUri
                ? remoteSession.Source.Uri.PathAndQuery
                : remoteSession.Source.Uri.OriginalString;

            var indexedFilePath = $"/api/indexed-files/{remoteFile.RemoteFileId}/";
            string proxyPath;

            if (remotePath.Contains(indexedFilePath))
            {
                var relativePath = remotePath[(remotePath.IndexOf(indexedFilePath) + indexedFilePath.Length)..];
                proxyPath = $"/api/remote-stream-sessions/{localSession.Id}/{relativePath}";
            }
            else
            {
                proxyPath = $"/api/remote-stream-sessions/{localSession.Id}/direct-stream";
            }

            localSource = new IndexedFileStreamUri
            {
                Uri = new Uri(proxyPath, UriKind.Relative),
                MimeType = remoteSession.Source.MimeType,
                StreamDecision = streamDecision
            };
        }

        activeStreamTracker.Upsert(localSession.Id, new ActiveStreamInfo
        {
            SessionId = localSession.Id,
            UserId = command.UserId,
            IdentityUserId = command.UserId.ToString(),
            DeviceId = device.Id,
            DeviceName = device.DeviceName,
            DeviceType = device.ClientType.ToString(),
            IndexedFileId = remoteFile.Id,
            StreamDecision = streamDecision,
            FederatedPlaybackExecution = assignedExecution,
            Duration = remoteFile.Duration?.TotalSeconds ?? 0,
            StartedAt = DateTime.UtcNow
        });

        var result = new StreamingSessionDto
        {
            Id = localSession.Id,
            IndexedFileId = remoteFile.Id,
            State = localSession.State,
            Position = localSession.Position,
            PlaybackSettings = remoteSession.PlaybackSettings ?? new PlaybackSettingsDto(),
            Source = localSource,
            AudioTracks = remoteSession.AudioTracks,
            SubtitleTracks = remoteSession.SubtitleTracks,
            SourceFrameRate = remoteSession.SourceFrameRate,
            SourceVideoWidth = remoteSession.SourceVideoWidth,
            SourceVideoHeight = remoteSession.SourceVideoHeight,
            StreamDecision = streamDecision,
            FederatedPlaybackExecution = assignedExecution
        };

        return new CreateRemoteStreamSessionResult(result, $"/api/remote-stream-sessions/{localSession.Id}");
    }

    private void WarmFederatedMediaCache(FederatedPlaybackSessionState state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var entry = await federatedMediaCache.EnsureOpenedAsync(
                    state.RemoteIndexedFileId,
                    state.PeerServerId,
                    state.RemoteSessionId,
                    state.OriginDirectStreamUrl);
                await federatedMediaCache.EnsureHeaderAndCuesAsync(entry);
                state.LocalMediaPath = entry.LocalPath;
                federatedSessionStore.Set(state);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Federated media cache warm-up failed for session {SessionId}",
                    state.LocalSessionId);
            }
        });
    }

    internal static bool ShouldServePeerHls(
        FederatedPlaybackExecution execution,
        StreamDecisionDto? decision) =>
        execution == FederatedPlaybackExecution.Peer
        && decision is not null
        && decision.Mode != PlaybackMode.Direct;

    internal static IndexedFileStreamUri BuildLocalHlsSource(Guid localSessionId, StreamDecisionDto decision)
    {
        var query = new Dictionary<string, string?>
        {
            ["StreamSessionId"] = localSessionId.ToString(),
            ["TranscodingVideoCodec"] = decision.Mode == PlaybackMode.Transcode
                || decision.Reason.HasFlag(TranscodeReason.HlsSegmentsUnavailable)
                    ? decision.StreamVideoCodec
                    : null,
            ["DefaultAudioTrackIndex"] = decision.SelectedAudioTrackIndex?.ToString(),
            ["DefaultSubtitleTrackIndex"] = decision.IsSubtitleBurnIn
                ? null
                : decision.SelectedSubtitleTrackIndex?.ToString(),
            ["SubtitleBurnInStreamIndex"] = decision.IsSubtitleBurnIn
                ? decision.SelectedSubtitleTrackIndex?.ToString()
                : null,
            // Ladder Names only (720p). Never pass WxH StreamResolution - it skips encode.
            ["Quality"] = decision.Reason.HasFlag(TranscodeReason.QualityDownscale)
                ? Constants.VideoQualities.Values
                    .FirstOrDefault(v =>
                        decision.StreamResolution is { } sr
                        && string.Equals($"{v.Width}x{v.Height}", sr, StringComparison.OrdinalIgnoreCase))
                    ?.Name
                : null,
            ["MaxAudioChannels"] = decision.StreamAudioChannels?.ToString(),
            ["VideoCodecsOnly"] = "true"
        };

        var filtered = query
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value);

        var path = $"/api/remote-stream-sessions/{localSessionId}/hls-stream/manifest.m3u8";
        var uri = filtered.Count > 0
            ? QueryHelpers.AddQueryString(path, filtered)
            : path;

        return new IndexedFileStreamUri
        {
            Uri = new Uri(uri, UriKind.Relative),
            MimeType = "application/vnd.apple.mpegurl",
            StreamDecision = decision
        };
    }
}
