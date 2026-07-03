using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Stats.Queries.GetWatchStats;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetWatchStatsQuery(MediaType? MediaType = null, string Period = "month", Guid? UserId = null, bool GlobalStats = false, DateTime? From = null, DateTime? To = null) : IRequest<WatchStatsDto>;

public class GetWatchStatsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetWatchStatsQuery, WatchStatsDto>
{
    public async Task<WatchStatsDto> Handle(GetWatchStatsQuery request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? (request.GlobalStats ? null : currentUser.Id);
        if (targetUserId is null && !request.GlobalStats)
            return new WatchStatsDto();

        var since = request.Period == "custom" ? request.From?.ToUniversalTime() : GetPeriodStart(request.Period);
        var until = request.Period == "custom" ? request.To?.ToUniversalTime() : null;
        var mediaTypes = GetMediaTypes(request.MediaType);

        var sessionsQuery = context.MediaPlaybackSessions.AsQueryable();

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

        if (until.HasValue)
        {
            sessionsWithMedia = sessionsWithMedia.Where(s => s.StartedAt <= until.Value);
        }

        var totalPlays = await sessionsWithMedia.Select(s => s.ReferenceId).Distinct().CountAsync(cancellationToken);
        var uniqueItems = await sessionsWithMedia.Select(s => s.MediaId).Distinct().CountAsync(cancellationToken);

        var totalSeconds = await sessionsWithMedia
            .SumAsync(s => s.WatchedDurationSeconds > 0 ? s.WatchedDurationSeconds : s.DurationSeconds, cancellationToken);

        var topItems = await sessionsWithMedia
            .Join(
                context.Medias,
                s => s.MediaId,
                m => m.Id,
                (s, m) => new { s.MediaId, s.ReferenceId, m.Title, m.Type })
            .GroupBy(x => new { x.MediaId, x.Title, x.Type })
            .Select(g => new TopItemDto
            {
                Id = g.Key.MediaId,
                Name = g.Key.Title ?? "Unknown",
                MediaType = g.Key.Type.ToString(),
                PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        var topGenres = new List<GenreStatDto>();

        var genreSessionPairs = await sessionsWithMedia
            .Join(
                context.Medias,
                s => s.MediaId,
                m => m.Id,
                (s, m) => new { s.ReferenceId, m.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (genreSessionPairs.Count > 0)
        {
            var genreMediaIds = genreSessionPairs.Select(x => x.Id).Distinct().ToHashSet();

            var mediaGenres = await context.Medias
                .Where(m => genreMediaIds.Contains(m.Id))
                .Select(m => new
                {
                    m.Id,
                    Genres = m.MetadataTags
                        .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                        .Select(mt => mt.MetadataTag.DisplayName)
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            var genreLookup = mediaGenres.ToDictionary(x => x.Id, x => (IList<string>)x.Genres);

            topGenres = genreSessionPairs
                .SelectMany(x => genreLookup.GetValueOrDefault(x.Id, []),
                    (x, genre) => new { x.ReferenceId, Genre = genre })
                .GroupBy(x => x.Genre)
                .Select(g => new GenreStatDto
                {
                    Genre = g.Key,
                    PlayCount = g.DistinctBy(x => x.ReferenceId).Count()
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToList();
        }

        var topArtists = new List<TopItemDto>();
        var topAlbums = new List<TopItemDto>();
        var topShows = new List<TopItemDto>();

        if (request.MediaType is null or MediaType.MusicTrack)
        {
            topArtists = await sessionsWithMedia
                .Join(
                    context.Medias.OfType<MusicTrack>(),
                    s => s.MediaId,
                    t => t.Id,
                    (s, t) => new { s.ReferenceId, ArtistId = t.ArtistId ?? t.Album!.ArtistId, ArtistTitle = t.Artist != null ? t.Artist.Title : t.Album!.Artist!.Title })
                .Where(x => x.ArtistId != null)
                .GroupBy(x => new { x.ArtistId, x.ArtistTitle })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.ArtistId!.Value,
                    Name = g.Key.ArtistTitle ?? "Unknown artist",
                    MediaType = nameof(MediaType.MusicArtist),
                    PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToListAsync(cancellationToken);

            topAlbums = await sessionsWithMedia
                .Join(
                    context.Medias.OfType<MusicTrack>(),
                    s => s.MediaId,
                    t => t.Id,
                    (s, t) => new { s.ReferenceId, t.AlbumId })
                .Join(
                    context.Medias.OfType<MusicAlbum>(),
                    x => x.AlbumId,
                    a => a.Id,
                    (x, a) => new { x.ReferenceId, a.Id, a.Title })
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Title ?? "Unknown album",
                    MediaType = nameof(MediaType.MusicAlbum),
                    PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                })
                .OrderByDescending(x => x.PlayCount)
                .Take(10)
                .ToListAsync(cancellationToken);
        }

        if (request.MediaType is null or MediaType.SerieEpisode)
        {
            topShows = await sessionsWithMedia
                .Join(
                    context.Medias.OfType<SerieEpisode>(),
                    s => s.MediaId,
                    e => e.Id,
                    (s, e) => new { s.ReferenceId, e.SerieId })
                .Join(
                    context.Medias.OfType<Serie>(),
                    x => x.SerieId,
                    se => se.Id,
                    (x, se) => new { x.ReferenceId, se.Id, se.Title })
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new TopItemDto
                {
                    Id = g.Key.Id,
                    Name = g.Key.Title ?? "Unknown show",
                    MediaType = nameof(MediaType.Serie),
                    PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
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

        topItems = (await MediaCoverPictureResolver.EnrichTopItemsAsync(
            context,
            topItems,
            item => item.Id,
            (item, imageUrl) => item with { ImageUrl = imageUrl },
            cancellationToken)).ToList();
        topArtists = (await MediaCoverPictureResolver.EnrichTopItemsAsync(
            context,
            topArtists,
            item => item.Id,
            (item, imageUrl) => item with { ImageUrl = imageUrl },
            cancellationToken)).ToList();
        topAlbums = (await MediaCoverPictureResolver.EnrichTopItemsAsync(
            context,
            topAlbums,
            item => item.Id,
            (item, imageUrl) => item with { ImageUrl = imageUrl },
            cancellationToken)).ToList();
        topShows = (await MediaCoverPictureResolver.EnrichTopItemsAsync(
            context,
            topShows,
            item => item.Id,
            (item, imageUrl) => item with { ImageUrl = imageUrl },
            cancellationToken)).ToList();

        var playbackDetails = request.MediaType == MediaType.MusicTrack
            ? null
            : await BuildPlaybackDetailsStatsAsync(sessionsWithMedia, cancellationToken);

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
            PlaysByHourOfDay = byHourOfDay,
            PlaybackDetails = playbackDetails
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

    private static async Task<PlaybackDetailsStatsDto?> BuildPlaybackDetailsStatsAsync(
        IQueryable<MediaPlaybackSession> sessions, CancellationToken cancellationToken)
    {
        var detailsQuery = sessions
            .Where(s => s.Details != null)
            .Select(s => s.Details!);

        var totalWithDetails = await detailsQuery.CountAsync(cancellationToken);
        if (totalWithDetails == 0)
            return null;

        var decisions = await detailsQuery
            .GroupBy(d => d.VideoDecision ?? "Unknown")
            .Select(g => new LabelCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var audioLanguages = await detailsQuery
            .Where(d => d.AudioTrackLanguage != null)
            .GroupBy(d => d.AudioTrackLanguage!)
            .Select(g => new LabelCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var subtitleLanguages = await detailsQuery
            .Where(d => d.SubtitleTrackLanguage != null)
            .GroupBy(d => d.SubtitleTrackLanguage!)
            .Select(g => new LabelCountDto { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var resolutions = await detailsQuery
            .Where(d => d.SourceVideoWidth != null && d.SourceVideoHeight != null)
            .GroupBy(d => d.SourceVideoHeight!.Value)
            .Select(g => new { Height = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var resolutionLabels = resolutions
            .Select(r => new LabelCountDto
            {
                Label = r.Height >= 2160 ? "4K" : r.Height >= 1080 ? "1080p" : r.Height >= 720 ? "720p" : $"{r.Height}p",
                Count = r.Count
            })
            .GroupBy(r => r.Label)
            .Select(g => new LabelCountDto { Label = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .ToList();

        var transcodeReasons = await detailsQuery
            .Where(d => d.TranscodeReason != null && d.TranscodeReason != TranscodeReason.None)
            .GroupBy(d => d.TranscodeReason!.Value)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var transcodeReasonLabels = transcodeReasons
            .Select(r => new LabelCountDto { Label = FormatTranscodeReason(r.Reason), Count = r.Count })
            .ToList();

        return new PlaybackDetailsStatsDto
        {
            PlaybackDecisions = decisions,
            TopAudioLanguages = audioLanguages,
            TopSubtitleLanguages = subtitleLanguages,
            TopResolutions = resolutionLabels,
            TopTranscodeReasons = transcodeReasonLabels
        };
    }

    private static string FormatTranscodeReason(TranscodeReason reason) => reason switch
    {
        TranscodeReason.VideoCodecNotSupported => "Video codec",
        TranscodeReason.AudioCodecNotSupported => "Audio codec",
        TranscodeReason.ContainerNotSupported => "Container",
        TranscodeReason.HlsSegmentsUnavailable => "HLS segments",
        TranscodeReason.SubtitlesBurnIn => "Subtitles burn-in",
        TranscodeReason.ResolutionNotSupported => "Resolution",
        _ => reason.ToString()
    };
}
