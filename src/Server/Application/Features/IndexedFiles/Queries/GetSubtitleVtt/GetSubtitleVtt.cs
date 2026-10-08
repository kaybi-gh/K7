using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleVtt;

public record GetSubtitleVttQuery(Guid Id, int SubtitleTrackIndex) : IRequest<HttpContentResult>;

public class GetSubtitleVttQueryHandler : IRequestHandler<GetSubtitleVttQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAccessGuard _accessGuard;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly ILogger<GetSubtitleVttQueryHandler> _logger;
    private readonly IPeerAuthorizationService _peerAuthorization;
    private readonly IPeerClient _peerClient;
    private readonly string _transcodingPath;

    public GetSubtitleVttQueryHandler(
        IApplicationDbContext context,
        IMediaAccessGuard accessGuard,
        IMediaTranscoder mediaTranscoder,
        ILogger<GetSubtitleVttQueryHandler> logger,
        IOptions<PathsConfiguration> pathsOptions,
        IPeerAuthorizationService peerAuthorization,
        IPeerClient peerClient)
    {
        _context = context;
        _accessGuard = accessGuard;
        _mediaTranscoder = mediaTranscoder;
        _logger = logger;
        _peerAuthorization = peerAuthorization;
        _peerClient = peerClient;
        _transcodingPath = pathsOptions.Value.Transcoding
            ?? throw new InvalidOperationException("Transcoding path not configured");
    }

    public async Task<HttpContentResult> Handle(GetSubtitleVttQuery query, CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureAccessByIndexedFileAsync(query.Id, cancellationToken);

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        if (entity is null)
            return await ProxyRemoteSubtitleAsync(query, cancellationToken);

        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is not VideoFileMetadata videoMetadata)
            return new EmptyHttpContentResult(404);

        await _context.Entry(videoMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

        var track = videoMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == query.SubtitleTrackIndex);
        if (track is null || !track.IsTextBased)
            return new EmptyHttpContentResult(404);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
            return new EmptyHttpContentResult(404);

        var vttCachePath = HlsSubtitleVttExtractor.GetCachePath(
            _transcodingPath,
            entity.Id,
            query.SubtitleTrackIndex);

        if (!HlsSubtitleVttExtractor.IsReady(vttCachePath))
        {
            // Never block: extract in the background and let the client poll. Blocking here
            // makes Android/web wait on ffmpeg before playback and stalls the request thread.
            _logger.LogDebug(
                "Subtitle VTT cache miss for file {IndexedFileId} track {Track} - extract in background",
                entity.Id,
                query.SubtitleTrackIndex);
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                _mediaTranscoder,
                entity.Path,
                query.SubtitleTrackIndex,
                vttCachePath,
                _logger);
            return new TextHttpContentResult(
                "Subtitle extract in progress",
                "text/plain",
                503);
        }

        var vtt = await File.ReadAllTextAsync(vttCachePath, cancellationToken);
        return new TextHttpContentResult(vtt, "text/vtt; charset=utf-8");
    }

    /// <summary>
    /// Remote library ids are not local files. The Windows sidecar asks
    /// indexed-files anyway, so forward that request to the origin session.
    /// </summary>
    private async Task<HttpContentResult> ProxyRemoteSubtitleAsync(
        GetSubtitleVttQuery query,
        CancellationToken cancellationToken)
    {
        var remote = await _context.RemoteIndexedFiles
            .AsNoTracking()
            .Include(r => r.PeerServer)
            .FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

        if (remote?.PeerServer is null)
            throw new NotFoundException(query.Id.ToString(), "IndexedFile");

        if (remote.PeerServer.Status != PeerStatus.Active)
            return new TextHttpContentResult("Peer unavailable", "text/plain", 503);

        var session = await _context.StreamSessions
            .AsNoTracking()
            .Where(s => s.RemoteIndexedFileId == remote.Id && s.RemoteSessionId != null)
            .OrderByDescending(s => s.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (session?.RemoteSessionId is null || session.PeerServerId is null)
            return new EmptyHttpContentResult(404);

        var auth = await _peerAuthorization.AuthenticateOutboundAsync(session.PeerServerId.Value, cancellationToken);
        if (auth is null)
            return new TextHttpContentResult("Peer auth failed", "text/plain", 503);

        var (peer, token) = auth.Value;
        using var response = await _peerClient.ProxyStreamContentAsync(
            peer.BaseUrl,
            token,
            session.RemoteSessionId.Value,
            $"subtitles/{query.SubtitleTrackIndex}.vtt",
            cancellationToken);

        var status = (int)response.StatusCode;
        if (status == 503)
            return new TextHttpContentResult("Subtitle extract in progress", "text/plain", 503);

        if (status is < 200 or >= 300)
            return new EmptyHttpContentResult(404);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug(
            "Proxied subtitle VTT for remote file {RemoteFileId} track {Track} bytes {Bytes}",
            remote.Id,
            query.SubtitleTrackIndex,
            body.Length);
        return new TextHttpContentResult(body, "text/vtt; charset=utf-8");
    }
}
