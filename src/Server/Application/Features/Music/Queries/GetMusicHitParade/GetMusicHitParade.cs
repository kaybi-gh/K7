using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Music.Queries.GetMusicHitParade;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetMusicHitParadeQuery : IRequest<MusicHitParadeDto>
{
    public string Period { get; init; } = MusicHitParadePeriods.All;
    public string Scope { get; init; } = MusicHitParadeScopes.Personal;
    public int Count { get; init; } = 50;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? Year { get; init; }
    public int? Month { get; init; }
    public string? Season { get; init; }
}

public class GetMusicHitParadeQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    LiteMediaProjectionService liteMediaProjection,
    MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetMusicHitParadeQuery, MusicHitParadeDto>
{
    public async Task<MusicHitParadeDto> Handle(
        GetMusicHitParadeQuery request,
        CancellationToken cancellationToken)
    {
        var period = MusicHitParadeCalendar.NormalizePeriod(request.Period);
        var scope = MusicHitParadeCalendar.NormalizeScope(request.Scope);
        var count = Math.Clamp(request.Count, 1, 100);
        var (since, until) = ResolveBounds(request, period);

        var empty = new MusicHitParadeDto
        {
            Period = period,
            Scope = scope,
            From = since,
            To = until
        };

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var sessionsQuery = BuildScopedSessionsQuery(scope, sharedProfileId);
        if (sessionsQuery is null)
            return empty;

        var periodSessions = sessionsQuery.Where(s => s.CompletedAt != null);

        if (since.HasValue)
            periodSessions = periodSessions.Where(s => s.StartedAt >= since.Value);
        if (until.HasValue)
            periodSessions = periodSessions.Where(s => s.StartedAt < until.Value);

        var accessibleTrackIds = await GetAccessibleMusicTrackIdsAsync(
            periodSessions,
            sharedProfileId,
            cancellationToken);

        var ranked = await periodSessions
            .Where(s => accessibleTrackIds.Contains(s.MediaId))
            .GroupBy(s => new { s.MediaId, s.ReferenceId })
            .Select(g => g.Key.MediaId)
            .GroupBy(id => id)
            .Select(g => new { MediaId = g.Key, PlayCount = g.Count() })
            .OrderByDescending(x => x.PlayCount)
            .ThenBy(x => x.MediaId)
            .Take(count)
            .ToListAsync(cancellationToken);

        if (ranked.Count == 0)
            return empty;

        var playCountById = ranked.ToDictionary(x => x.MediaId, x => x.PlayCount);
        var liteTracks = await liteMediaProjection.GetLiteMediaDtosAsync(
            ranked.Select(x => x.MediaId).ToList(),
            currentUser.Id,
            cancellationToken);
        var liteById = liteTracks
            .OfType<LiteMusicTrackDto>()
            .ToDictionary(t => t.Id);

        var tracks = ranked
            .Where(x => liteById.ContainsKey(x.MediaId))
            .Select(x => new PlayedMusicTrackDto
            {
                Track = liteById[x.MediaId],
                PlayCount = playCountById[x.MediaId]
            })
            .ToList();

        return new MusicHitParadeDto
        {
            Period = period,
            Scope = scope,
            From = since,
            To = until,
            Tracks = tracks
        };
    }

    private IQueryable<MediaPlaybackSession>? BuildScopedSessionsQuery(string scope, Guid? sharedProfileId)
    {
        var sessionsQuery = context.MediaPlaybackSessions.AsNoTracking().AsQueryable();
        if (scope == MusicHitParadeScopes.Server)
            return sessionsQuery;

        var userId = currentUser.Id;
        if (userId is null && sharedProfileId is null)
            return null;

        if (sharedProfileId is { } profileId)
            return sessionsQuery.Where(s => s.SharedProfileId == profileId);

        if (userId is not { } id)
            return null;

        var coViewerReferenceIds = context.MediaPlaybackSessionCoViewers
            .Where(c => c.UserId == id)
            .Select(c => c.ReferenceId);

        return sessionsQuery.Where(s =>
            s.UserId == id
            || coViewerReferenceIds.Contains(s.ReferenceId));
    }

    private async Task<IQueryable<Guid>> GetAccessibleMusicTrackIdsAsync(
        IQueryable<MediaPlaybackSession> periodSessions,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        var sessionMediaIds = periodSessions.Select(s => s.MediaId);

        IQueryable<BaseMedia> tracks = context.Medias
            .AsNoTracking()
            .Where(m => m.Type == MediaType.MusicTrack && sessionMediaIds.Contains(m.Id))
            .WhereHasLibraryAvailability(context);

        if (currentUser.Id is { } userId)
        {
            tracks = await mediaAccessFilter.ApplyAllAsync(tracks, userId, sharedProfileId, cancellationToken);
        }
        else
        {
            tracks = mediaAccessFilter.ApplyUnavailablePeerExclusion(tracks);
        }

        return tracks.Select(m => m.Id);
    }

    private static (DateTime? Since, DateTime? Until) ResolveBounds(
        GetMusicHitParadeQuery request,
        string period)
    {
        if (request.From is not null || request.To is not null)
            return (ToUtc(request.From), ToUtc(request.To));

        if (period == MusicHitParadePeriods.All || period == MusicHitParadePeriods.Custom)
            return (null, null);

        var now = DateTime.UtcNow;
        var year = request.Year ?? now.Year;
        var month = request.Month ?? now.Month;
        var window = MusicHitParadeCalendar.GetWindow(period, year, month, request.Season, TimeZoneInfo.Utc);
        return (window?.From.UtcDateTime, window?.To.UtcDateTime);
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
            return null;

        var dt = value.Value;
        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return dt.ToUniversalTime();
    }
}
