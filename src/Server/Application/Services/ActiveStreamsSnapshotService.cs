using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public interface IActiveStreamsSnapshotService
{
    Task<IReadOnlyList<ActiveStreamDto>> BuildAsync(CancellationToken cancellationToken = default);
}

public sealed class ActiveStreamsSnapshotService(
    IActiveStreamTracker tracker,
    IApplicationDbContext context,
    IFfmpegCapabilitiesService ffmpegCapabilitiesService,
    ILogger<ActiveStreamsSnapshotService> logger) : IActiveStreamsSnapshotService
{
    public async Task<IReadOnlyList<ActiveStreamDto>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var activeStreams = tracker.GetActiveStreams();
        if (activeStreams.Count == 0)
            return [];

        var userIds = activeStreams
            .Where(s => s.UserId.HasValue)
            .Select(s => s.UserId!.Value)
            .Distinct()
            .ToList();

        var avatarMap = userIds.Count > 0
            ? await context.MetadataPictures
                .AsNoTracking()
                .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
                .Select(p => new { p.UserId, p.Id })
                .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken)
            : [];

        var streams = new List<ActiveStreamDto>(activeStreams.Count);

        foreach (var s in activeStreams)
        {
            await StreamDecisionHydrator.TryHydrateTrackerAsync(
                s.SessionId,
                tracker,
                context,
                ffmpegCapabilitiesService,
                logger,
                cancellationToken);

            await StreamDecisionEnrichment.TryEnrichAndUpdateTrackerAsync(
                s.SessionId,
                tracker,
                ffmpegCapabilitiesService,
                cancellationToken);

            var streamDecision = tracker.GetStreamInfo(s.SessionId)?.StreamDecision ?? s.StreamDecision;

            streams.Add(new ActiveStreamDto
            {
                ConnectionId = s.SessionId.ToString(),
                UserId = s.UserId,
                UserName = s.UserName,
                UserAvatarUrl = s.UserId.HasValue && avatarMap.TryGetValue(s.UserId.Value, out var picId)
                    ? $"/api/metadata-pictures/{picId}"
                    : null,
                MediaId = s.MediaId,
                MediaTitle = s.MediaTitle,
                MediaType = s.MediaType,
                ParentId = s.ParentId,
                DeviceId = s.DeviceId,
                DeviceName = s.DeviceName,
                DeviceType = s.DeviceType,
                ThumbnailUrl = s.ThumbnailUrl,
                StreamDecision = streamDecision,
                StartedAt = s.StartedAt,
                Position = s.Position,
                Duration = s.Duration,
                State = s.State,
                SharedProfileName = s.SharedProfileName
            });
        }

        return streams;
    }
}
