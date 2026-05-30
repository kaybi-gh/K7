using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Stats.Queries.GetPlaybackHistory;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetPlaybackHistoryQuery : IRequest<PlaybackHistoryPageDto>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public MediaType? MediaType { get; init; }
    public Guid? UserId { get; init; }
    public bool IncludeStreamQuality { get; init; }
    public bool ShowAllUsers { get; init; }
}

public class GetPlaybackHistoryQueryHandler(IApplicationDbContext context, IUser currentUser, IIdentityService identityService)
    : IRequestHandler<GetPlaybackHistoryQuery, PlaybackHistoryPageDto>
{
    public async Task<PlaybackHistoryPageDto> Handle(GetPlaybackHistoryQuery request, CancellationToken cancellationToken)
    {
        IQueryable<MediaPlaybackSession> sessionsQuery;

        if (request.ShowAllUsers)
        {
            sessionsQuery = context.MediaPlaybackSessions.AsQueryable();

            if (request.UserId.HasValue)
                sessionsQuery = sessionsQuery.Where(s => s.UserId == request.UserId.Value);
        }
        else
        {
            var targetUserId = request.UserId ?? currentUser.Id;
            if (targetUserId is null)
                return new PlaybackHistoryPageDto();

            sessionsQuery = context.MediaPlaybackSessions
                .Where(s => s.UserId == targetUserId.Value);
        }

        if (request.MediaType.HasValue)
        {
            sessionsQuery = sessionsQuery
                .Where(s => s.Media.Type == request.MediaType.Value);
        }

        var groupedQuery = sessionsQuery
            .GroupBy(s => s.ReferenceId)
            .Select(g => new
            {
                ReferenceId = g.Key,
                StartedAt = g.Min(s => s.StartedAt),
                StoppedAt = g.Max(s => s.StoppedAt),
                TotalWatchedSeconds = g.Sum(s => s.WatchedDurationSeconds > 0 ? s.WatchedDurationSeconds : s.DurationSeconds),
                SegmentCount = g.Count(),
                IsCompleted = g.Any(s => s.CompletedAt != null),
                MediaId = g.First().MediaId,
                DeviceId = g.First().DeviceId,
                UserId = g.First().UserId
            });

        var totalCount = await groupedQuery.CountAsync(cancellationToken);

        var pagedGroups = await groupedQuery
            .OrderByDescending(g => g.StartedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var mediaIds = pagedGroups.Select(g => g.MediaId).Distinct().ToList();
        var medias = await context.Medias
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Title, m.Type })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var libraryByMedia = await context.IndexedFiles
            .Where(f => f.MediaId != null && mediaIds.Contains(f.MediaId.Value))
            .Join(context.Libraries, f => f.LibraryId, l => l.Id, (f, l) => new { f.MediaId, LibraryName = l.Title })
            .GroupBy(x => x.MediaId!.Value)
            .Select(g => new { MediaId = g.Key, LibraryName = g.First().LibraryName })
            .ToDictionaryAsync(x => x.MediaId, x => x.LibraryName, cancellationToken);

        var deviceIds = pagedGroups.Where(g => g.DeviceId.HasValue).Select(g => g.DeviceId!.Value).Distinct().ToList();
        var devices = deviceIds.Count > 0
            ? await context.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DeviceName, d.ClientType })
                .ToDictionaryAsync(d => d.Id, cancellationToken)
            : [];

        var userIds = pagedGroups.Select(g => g.UserId).Distinct().ToList();
        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.IdentityUserId })
            .ToListAsync(cancellationToken);

        var userNames = new Dictionary<Guid, string?>();
        foreach (var u in users)
        {
            var name = u.DisplayName;
            if (name is null && u.IdentityUserId is not null)
                name = await identityService.GetUserNameAsync(u.IdentityUserId);
            userNames[u.Id] = name;
        }

        Dictionary<Guid, Domain.Entities.Users.PlaybackSessionDetails?>? detailsByRef = null;
        if (request.IncludeStreamQuality)
        {
            var referenceIds = pagedGroups.Select(g => g.ReferenceId).ToList();
            var sessionIds = await context.MediaPlaybackSessions
                .Where(s => referenceIds.Contains(s.ReferenceId))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var details = await context.PlaybackSessionDetails
                .Where(d => sessionIds.Contains(d.MediaPlaybackSessionId))
                .Include(d => d.MediaPlaybackSession)
                .ToListAsync(cancellationToken);

            detailsByRef = details
                .GroupBy(d => d.MediaPlaybackSession.ReferenceId)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault());
        }

        var items = pagedGroups.Select(g =>
        {
            var media = medias.GetValueOrDefault(g.MediaId);
            StreamQualityDto? quality = null;

            if (detailsByRef is not null && detailsByRef.TryGetValue(g.ReferenceId, out var details) && details is not null)
            {
                quality = new StreamQualityDto
                {
                    IsTranscode = details.IsTranscode,
                    VideoDecision = details.VideoDecision,
                    AudioDecision = details.AudioDecision,
                    TranscodeReason = details.TranscodeReason?.ToString(),
                    SourceResolution = details.SourceVideoWidth.HasValue && details.SourceVideoHeight.HasValue
                        ? $"{details.SourceVideoWidth}x{details.SourceVideoHeight}" : null,
                    SourceVideoCodec = details.SourceVideoCodec,
                    SourceAudioCodec = details.SourceAudioCodec,
                    StreamVideoCodec = details.StreamVideoCodec,
                    StreamAudioCodec = details.StreamAudioCodec,
                    Resolution = details.SourceVideoWidth.HasValue && details.SourceVideoHeight.HasValue
                        ? $"{details.SourceVideoWidth}x{details.SourceVideoHeight}" : null,
                    VideoCodec = details.StreamVideoCodec ?? details.SourceVideoCodec,
                    AudioCodec = details.StreamAudioCodec ?? details.SourceAudioCodec,
                    Bitrate = details.Bitrate,
                    AudioTrackLanguage = details.AudioTrackLanguage,
                    AudioTrackTitle = details.AudioTrackTitle,
                    AudioChannelLayout = details.AudioChannelLayout,
                    SubtitleTrackLanguage = details.SubtitleTrackLanguage,
                    SubtitleTrackTitle = details.SubtitleTrackTitle
                };
            }

            return new PlaybackHistoryItemDto
            {
                ReferenceId = g.ReferenceId,
                MediaId = g.MediaId,
                MediaTitle = media?.Title,
                MediaType = media?.Type.ToString(),
                LibraryName = libraryByMedia.GetValueOrDefault(g.MediaId),
                StartedAt = g.StartedAt,
                StoppedAt = g.StoppedAt,
                TotalWatchedSeconds = g.TotalWatchedSeconds,
                SegmentCount = g.SegmentCount,
                DeviceName = g.DeviceId.HasValue && devices.TryGetValue(g.DeviceId.Value, out var dev) ? dev.DeviceName : null,
                DeviceClient = g.DeviceId.HasValue && devices.TryGetValue(g.DeviceId.Value, out var devClient) ? devClient.ClientType.ToString() : null,
                IsCompleted = g.IsCompleted,
                UserName = userNames.GetValueOrDefault(g.UserId),
                StreamQuality = quality
            };
        }).ToList();

        return new PlaybackHistoryPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
