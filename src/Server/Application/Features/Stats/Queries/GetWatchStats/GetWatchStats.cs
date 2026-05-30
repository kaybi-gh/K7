using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Stats.Queries.GetWatchStats;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetWatchStatsQuery(MediaType? MediaType = null, string Period = "month", Guid? UserId = null, bool GlobalStats = false) : IRequest<WatchStatsDto>;

public class GetWatchStatsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetWatchStatsQuery, WatchStatsDto>
{
    public async Task<WatchStatsDto> Handle(GetWatchStatsQuery request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? (request.GlobalStats ? null : currentUser.Id);
        if (targetUserId is null && !request.GlobalStats)
            return new WatchStatsDto();

        var since = GetPeriodStart(request.Period);
        var mediaTypes = GetMediaTypes(request.MediaType);

        var statesQuery = context.UserMediaStates
            .Where(s => s.PlayCount > 0);

        if (targetUserId is not null)
            statesQuery = statesQuery.Where(s => s.UserId == targetUserId.Value);

        var statesWithMedia = statesQuery
            .Join(
                context.Medias.Where(m => mediaTypes.Contains(m.Type)),
                s => s.MediaId,
                m => m.Id,
                (s, m) => new { s.MediaId, s.PlayCount, Media = m });

        var totalPlays = await statesWithMedia.SumAsync(s => s.PlayCount, cancellationToken);
        var uniqueItems = await statesWithMedia.CountAsync(cancellationToken);

        var sessionsQuery = context.MediaPlaybackSessions
            .Where(s => s.CompletedAt != null);

        if (targetUserId is not null)
            sessionsQuery = sessionsQuery.Where(s => s.UserId == targetUserId.Value);

        var sessionsWithMedia = sessionsQuery
            .Join(
                context.Medias.Where(m => mediaTypes.Contains(m.Type)),
                s => s.MediaId,
                m => m.Id,
                (s, _) => s);

        if (since.HasValue)
        {
            sessionsWithMedia = sessionsWithMedia.Where(s => s.StartedAt >= since.Value);
        }

        var totalSeconds = await sessionsWithMedia
            .SumAsync(s => s.WatchedDurationSeconds > 0 ? s.WatchedDurationSeconds : s.DurationSeconds, cancellationToken);

        var topItems = await statesWithMedia
            .Select(s => new TopItemDto
            {
                Id = s.MediaId,
                Name = s.Media.Title ?? "Unknown",
                PlayCount = s.PlayCount
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        var topGenres = await statesWithMedia
            .SelectMany(s => s.Media.Genres, (s, genre) => new { s.PlayCount, Genre = genre })
            .GroupBy(x => x.Genre)
            .Select(g => new GenreStatDto
            {
                Genre = g.Key,
                PlayCount = g.Sum(x => x.PlayCount)
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        var topArtists = new List<TopItemDto>();
        var topAlbums = new List<TopItemDto>();
        var topShows = new List<TopItemDto>();

        if (request.MediaType is null or MediaType.MusicTrack)
        {
            var musicStatesBase = context.UserMediaStates
                .Where(s => s.PlayCount > 0);

            if (targetUserId is not null)
                musicStatesBase = musicStatesBase.Where(s => s.UserId == targetUserId.Value);

            var musicStates = musicStatesBase
                .Join(
                    context.Medias.Where(m => m.Type == MediaType.MusicTrack),
                    s => s.MediaId,
                    m => m.Id,
                    (s, m) => new { s.MediaId, s.PlayCount, Media = m });

            topArtists = await musicStates
                .Join(
                    context.Medias.OfType<MusicTrack>(),
                    s => s.MediaId,
                    t => t.Id,
                    (s, t) => new { s.PlayCount, ArtistId = t.ArtistId ?? t.Album!.ArtistId, ArtistTitle = t.Artist != null ? t.Artist.Title : t.Album!.Artist!.Title })
                .Where(x => x.ArtistId != null)
                .GroupBy(x => new { x.ArtistId, x.ArtistTitle })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.ArtistId!.Value,
                    Name = g.Key.ArtistTitle ?? "Unknown artist",
                    PlayCount = g.Sum(x => x.PlayCount)
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToListAsync(cancellationToken);

            topAlbums = await musicStates
                .Join(
                    context.Medias.OfType<MusicTrack>(),
                    s => s.MediaId,
                    t => t.Id,
                    (s, t) => new { s.PlayCount, t.AlbumId })
                .Join(
                    context.Medias.OfType<MusicAlbum>(),
                    x => x.AlbumId,
                    a => a.Id,
                    (x, a) => new { x.PlayCount, a.Id, a.Title })
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Title ?? "Unknown album",
                    PlayCount = g.Sum(x => x.PlayCount)
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToListAsync(cancellationToken);
        }

        if (request.MediaType is null or MediaType.SerieEpisode)
        {
            var showStatesBase = context.UserMediaStates
                .Where(s => s.PlayCount > 0);

            if (targetUserId is not null)
                showStatesBase = showStatesBase.Where(s => s.UserId == targetUserId.Value);

            topShows = await showStatesBase
                .Join(
                    context.Medias.OfType<SerieEpisode>(),
                    s => s.MediaId,
                    e => e.Id,
                    (s, e) => new { s.PlayCount, e.SerieId })
                .Join(
                    context.Medias.OfType<Serie>(),
                    x => x.SerieId,
                    se => se.Id,
                    (x, se) => new { x.PlayCount, se.Id, se.Title })
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Title ?? "Unknown show",
                    PlayCount = g.Sum(x => x.PlayCount)
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToListAsync(cancellationToken);
        }

        var timeSeries = await BuildTimeSeriesAsync(sessionsWithMedia, request.Period, cancellationToken);
        var byDayOfWeek = await BuildByDayOfWeekAsync(sessionsWithMedia, cancellationToken);
        var byHourOfDay = await BuildByHourOfDayAsync(sessionsWithMedia, cancellationToken);

        var topDevices = await sessionsWithMedia
            .Where(s => s.DeviceId != null)
            .GroupBy(s => new { s.DeviceId, s.Device!.DeviceName })
            .Select(g => new TopItemDto
            {
                Id = g.Key.DeviceId!.Value,
                Name = g.Key.DeviceName ?? "Unknown device",
                PlayCount = g.Select(s => s.ReferenceId).Distinct().Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new WatchStatsDto
        {
            Period = request.Period,
            TotalWatchTimeHours = Math.Round(totalSeconds / 3600.0, 1),
            TotalPlays = totalPlays,
            UniqueItemsPlayed = uniqueItems,
            TopItems = topItems,
            TopArtists = topArtists,
            TopAlbums = topAlbums,
            TopShows = topShows,
            TopGenres = topGenres,
            TopDevices = topDevices,
            PlaysOverTime = timeSeries,
            PlaysByDayOfWeek = byDayOfWeek,
            PlaysByHourOfDay = byHourOfDay
        };
    }

    private static DateTime? GetPeriodStart(string period) => period switch
    {
        "week" => DateTime.UtcNow.AddDays(-7),
        "month" => DateTime.UtcNow.AddMonths(-1),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };

    private static MediaType[] GetMediaTypes(MediaType? filter) => filter switch
    {
        MediaType.MusicTrack => [MediaType.MusicTrack],
        MediaType.Movie => [MediaType.Movie],
        MediaType.SerieEpisode => [MediaType.SerieEpisode],
        _ => [MediaType.MusicTrack, MediaType.Movie, MediaType.SerieEpisode]
    };

    private static async Task<List<TimeSeriesPointDto>> BuildTimeSeriesAsync(
        IQueryable<MediaPlaybackSession> sessions, string period, CancellationToken cancellationToken)
    {
        var data = await sessions
            .GroupBy(s => s.StartedAt.Date)
            .Select(g => new TimeSeriesPointDto
            {
                Date = g.Key,
                Count = g.Select(s => s.ReferenceId).Distinct().Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return data;
    }

    private static async Task<List<DayOfWeekPointDto>> BuildByDayOfWeekAsync(
        IQueryable<MediaPlaybackSession> sessions, CancellationToken cancellationToken)
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        var data = await sessions
            .GroupBy(s => (int)s.StartedAt.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Select(s => s.ReferenceId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, 7)
            .Select(d => new DayOfWeekPointDto
            {
                Day = d,
                Name = dayNames[d],
                Count = data.FirstOrDefault(x => x.Day == d)?.Count ?? 0
            })
            .ToList();
    }

    private static async Task<List<HourOfDayPointDto>> BuildByHourOfDayAsync(
        IQueryable<MediaPlaybackSession> sessions, CancellationToken cancellationToken)
    {
        var data = await sessions
            .GroupBy(s => s.StartedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Select(s => s.ReferenceId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, 24)
            .Select(h => new HourOfDayPointDto
            {
                Hour = h,
                Count = data.FirstOrDefault(x => x.Hour == h)?.Count ?? 0
            })
            .ToList();
    }
}
