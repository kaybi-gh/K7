using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.MediaShares.Commands.ReportShareProgress;

/// <summary>
/// Player progress reported by a share visitor. It is recorded like the progress of a guest,
/// so administrators see the stream live and in the playback history (under the share
/// visitor account, on the technical device of the visitor session). It also closes a share
/// set to close once its media was played to the end.
/// Everything comes from the visitor browser, so this is a convenience and not a
/// guarantee: expiration and access limits remain the real barriers.
/// </summary>
public record ReportShareProgressCommand : IRequest
{
    public required Guid ShareId { get; init; }
    public required Guid MediaId { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? ReferenceId { get; init; }
    public int? State { get; init; }
    public Guid? IndexedFileId { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }
}

public class ReportShareProgressCommandHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess,
    IActiveStreamTracker activeStreamTracker,
    ISender sender)
    : IRequestHandler<ReportShareProgressCommand>
{
    public async Task Handle(ReportShareProgressCommand request, CancellationToken cancellationToken)
    {
        var grant = shareVisitorAccess.RequireGrant(request.ShareId);

        // A media outside the share is ignored: nothing to record, nothing to close.
        var matches = await shareVisitorAccess.GetMatchingSharesAsync(request.MediaId, cancellationToken);
        var match = matches.FirstOrDefault(m => m.ShareId == request.ShareId);
        if (match is null)
            return;

        if (match.AllowPlayback)
            await RecordPlaybackAsync(request, grant, cancellationToken);

        if (request.DurationSeconds <= 0
            || request.PositionSeconds / request.DurationSeconds < MediaShareRules.ConsumedProgressRatio)
        {
            return;
        }

        await ShareConsumption.MarkConsumedAsync(context, request.ShareId, request.MediaId, cancellationToken);
    }

    private async Task RecordPlaybackAsync(
        ReportShareProgressCommand request,
        ShareVisitorGrant grant,
        CancellationToken cancellationToken)
    {
        if (request.SessionId is not { } sessionId
            || sessionId == Guid.Empty
            || request.State is not { } rawState
            || !Enum.IsDefined(typeof(PlaybackState), rawState)
            || !double.IsFinite(request.PositionSeconds)
            || !double.IsFinite(request.DurationSeconds)
            || request.PositionSeconds < 0
            || request.DurationSeconds < 0)
        {
            return;
        }

        // The technical device exists once the visitor created a stream session.
        var deviceId = await context.MediaShareSessions
            .AsNoTracking()
            .Where(s => s.Id == grant.SessionId)
            .Select(s => s.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (deviceId is not { } visitorDeviceId)
            return;

        // The session id comes from the visitor: it must never touch a playback session or
        // a live stream that belongs to someone else.
        var recorded = await context.MediaPlaybackSessions
            .AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .Select(s => new { s.DeviceId })
            .FirstOrDefaultAsync(cancellationToken);
        if (recorded is not null && recorded.DeviceId != visitorDeviceId)
            return;

        if (activeStreamTracker.GetStreamInfo(sessionId) is { DeviceId: { } liveDeviceId }
            && liveDeviceId != visitorDeviceId)
        {
            return;
        }

        var indexedFileId = request.IndexedFileId is { } fileId
            && await context.IndexedFiles
                .AsNoTracking()
                .AnyAsync(f => f.Id == fileId && f.MediaId == request.MediaId, cancellationToken)
            ? fileId
            : (Guid?)null;

        await sender.Send(
            new UpdatePlaybackProgressCommand(
                request.MediaId,
                sessionId,
                request.ReferenceId is { } referenceId && referenceId != Guid.Empty ? referenceId : sessionId,
                request.PositionSeconds,
                request.DurationSeconds,
                (PlaybackState)rawState,
                DeviceId: visitorDeviceId,
                AudioTrackIndex: request.AudioTrackIndex,
                SubtitleTrackIndex: request.SubtitleTrackIndex,
                IndexedFileId: indexedFileId),
            cancellationToken);
    }
}
