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
        var sharedProfileId = request.GlobalStats
            ? null
            : await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var targetUserId = request.UserId ?? (request.GlobalStats ? null : currentUser.Id);
        if (targetUserId is null && sharedProfileId is null && !request.GlobalStats)
            return new WatchStatsDto();

        var since = request.Period == "custom" ? request.From?.ToUniversalTime() : GetPeriodStart(request.Period);
        var until = request.Period == "custom" ? request.To?.ToUniversalTime() : null;
        var mediaTypes = GetMediaTypes(request.MediaType);

        var sessionsQuery = context.MediaPlaybackSessions.AsQueryable();

        if (request.GlobalStats)
        {
            if (targetUserId is not null)
                sessionsQuery = sessionsQuery.Where(s => s.UserId == targetUserId.Value);
        }
        else if (sharedProfileId is { } profileId)
        {
            sessionsQuery = sessionsQuery.Where(s => s.SharedProfileId == profileId);
        }
        else if (targetUserId is not null)
        {
            sessionsQuery = sessionsQuery.Where(s => s.SharedProfileId == null && s.UserId == targetUserId.Value);
        }

        var sessionsWithMedia = sessionsQuery
            .Join(
                context.Medias.Where(m => mediaTypes.Contains(m.Type)),
                s => s.MediaId,
                m => m.Id,
                (s, _) => s);

        if (since.HasValue)
            sessionsWithMedia = sessionsWithMedia.Where(s => s.StartedAt >= since.Value);

        if (until.HasValue)
            sessionsWithMedia = sessionsWithMedia.Where(s => s.StartedAt <= until.Value);

        var sessions = await sessionsWithMedia
            .Select(s => new SessionRow(
                s.MediaId,
                s.ReferenceId,
                s.StartedAt,
                s.WatchedDurationSeconds,
                s.DurationSeconds,
                s.DeviceId,
                s.Device != null ? s.Device.DeviceName : null))
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return new WatchStatsDto
            {
                Period = request.Period,
                PlaysByDayOfWeek = BuildEmptyDayOfWeek(),
                PlaysByHourOfDay = BuildEmptyHourOfDay()
            };
        }

        var totalPlays = sessions.Select(s => s.ReferenceId).Distinct().Count();
        var uniqueItems = sessions.Select(s => s.MediaId).Distinct().Count();
        var totalSeconds = sessions.Sum(s => s.WatchedDurationSeconds > 0 ? s.WatchedDurationSeconds : s.DurationSeconds);

        var mediaIds = sessions.Select(s => s.MediaId).Distinct().ToList();
        var mediaLookup = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new MediaInfo(m.Id, m.Title, m.Type))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var topItems = sessions
            .GroupBy(s => s.MediaId)
            .Select(g =>
            {
                var media = mediaLookup[g.Key];
                return new TopItemDto
                {
                    Id = g.Key,
                    Name = media.Title ?? "Unknown",
                    MediaType = media.Type.ToString(),
                    PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                };
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToList();

        var topGenres = await BuildTopGenresAsync(sessions, mediaIds, cancellationToken);

        var topArtists = new List<TopItemDto>();
        var topAlbums = new List<TopItemDto>();
        var topShows = new List<TopItemDto>();

        if (request.MediaType is null or MediaType.MusicTrack)
        {
            var trackIds = mediaLookup.Values
                .Where(m => m.Type == MediaType.MusicTrack)
                .Select(m => m.Id)
                .ToList();

            if (trackIds.Count > 0)
            {
                var trackRelations = await context.Medias
                    .AsNoTracking()
                    .OfType<MusicTrack>()
                    .Where(t => trackIds.Contains(t.Id))
                    .Select(t => new TrackRelation(
                        t.Id,
                        t.ArtistId ?? t.Album!.ArtistId,
                        t.Artist != null ? t.Artist.Title : t.Album!.Artist!.Title,
                        t.AlbumId,
                        t.Album!.Title))
                    .ToDictionaryAsync(t => t.TrackId, cancellationToken);

                topArtists = sessions
                    .Where(s => trackRelations.ContainsKey(s.MediaId))
                    .Select(s => new { s.ReferenceId, Relation = trackRelations[s.MediaId] })
                    .Where(x => x.Relation.ArtistId.HasValue)
                    .GroupBy(x => new { ArtistId = x.Relation.ArtistId!.Value, x.Relation.ArtistTitle })
                    .Select(g => new TopItemDto
                    {
                        Id = g.Key.ArtistId,
                        Name = g.Key.ArtistTitle ?? "Unknown artist",
                        MediaType = nameof(MediaType.MusicArtist),
                        PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.PlayCount)
                    .Take(10)
                    .ToList();

                topAlbums = sessions
                    .Where(s => trackRelations.ContainsKey(s.MediaId))
                    .Select(s => new { s.ReferenceId, Relation = trackRelations[s.MediaId] })
                    .Where(x => x.Relation.AlbumId.HasValue)
                    .GroupBy(x => new { AlbumId = x.Relation.AlbumId!.Value, x.Relation.AlbumTitle })
                    .Select(g => new TopItemDto
                    {
                        Id = g.Key.AlbumId,
                        Name = g.Key.AlbumTitle ?? "Unknown album",
                        MediaType = nameof(MediaType.MusicAlbum),
                        PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.PlayCount)
                    .Take(10)
                    .ToList();
            }
        }

        if (request.MediaType is null or MediaType.SerieEpisode)
        {
            var episodeIds = mediaLookup.Values
                .Where(m => m.Type == MediaType.SerieEpisode)
                .Select(m => m.Id)
                .ToList();

            if (episodeIds.Count > 0)
            {
                var episodeRelations = await context.Medias
                    .AsNoTracking()
                    .OfType<SerieEpisode>()
                    .Where(e => episodeIds.Contains(e.Id))
                    .Select(e => new EpisodeRelation(e.Id, e.SerieId, e.Serie!.Title))
                    .ToDictionaryAsync(e => e.EpisodeId, cancellationToken);

                topShows = sessions
                    .Where(s => episodeRelations.ContainsKey(s.MediaId))
                    .Select(s => new { s.ReferenceId, Relation = episodeRelations[s.MediaId] })
                    .GroupBy(x => new { x.Relation.SerieId, x.Relation.SerieTitle })
                    .Select(g => new TopItemDto
                    {
                        Id = g.Key.SerieId,
                        Name = g.Key.SerieTitle ?? "Unknown show",
                        MediaType = nameof(MediaType.Serie),
                        PlayCount = g.Select(x => x.ReferenceId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.PlayCount)
                    .Take(10)
                    .ToList();
            }
        }

        var timeSeries = BuildTimeSeries(sessions);
        var byDayOfWeek = BuildByDayOfWeek(sessions);
        var byHourOfDay = BuildByHourOfDay(sessions);

        var topDevices = sessions
            .Where(s => s.DeviceId.HasValue)
            .GroupBy(s => new { s.DeviceId, s.DeviceName })
            .Select(g => new TopItemDto
            {
                Id = g.Key.DeviceId!.Value,
                Name = g.Key.DeviceName ?? "Unknown device",
                PlayCount = g.Select(s => s.ReferenceId).Distinct().Count()
            })
            .OrderByDescending(x => x.PlayCount)
            .Take(10)
            .ToList();

        var imageUrls = await MediaCoverPictureResolver.GetCoverImageUrlsByMediaIdAsync(
            context,
            topItems.Select(i => i.Id)
                .Concat(topArtists.Select(i => i.Id))
                .Concat(topAlbums.Select(i => i.Id))
                .Concat(topShows.Select(i => i.Id))
                .Distinct()
                .ToList(),
            cancellationToken);

        topItems = topItems.Select(i => i with { ImageUrl = imageUrls.GetValueOrDefault(i.Id) }).ToList();
        topArtists = topArtists.Select(i => i with { ImageUrl = imageUrls.GetValueOrDefault(i.Id) }).ToList();
        topAlbums = topAlbums.Select(i => i with { ImageUrl = imageUrls.GetValueOrDefault(i.Id) }).ToList();
        topShows = topShows.Select(i => i with { ImageUrl = imageUrls.GetValueOrDefault(i.Id) }).ToList();

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

    private async Task<List<GenreStatDto>> BuildTopGenresAsync(
        IReadOnlyList<SessionRow> sessions,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var mediaGenres = await context.Medias
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
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

        return sessions
            .SelectMany(s => genreLookup.GetValueOrDefault(s.MediaId, []),
                (s, genre) => new { s.ReferenceId, Genre = genre })
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

    private static List<TimeSeriesPointDto> BuildTimeSeries(IReadOnlyList<SessionRow> sessions) =>
        sessions
            .GroupBy(s => s.StartedAt.Date)
            .Select(g => new TimeSeriesPointDto
            {
                Date = g.Key,
                Count = g.Select(s => s.ReferenceId).Distinct().Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

    private static List<DayOfWeekPointDto> BuildByDayOfWeek(IReadOnlyList<SessionRow> sessions)
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var data = sessions
            .GroupBy(s => (int)s.StartedAt.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Select(s => s.ReferenceId).Distinct().Count() })
            .ToList();

        return Enumerable.Range(0, 7)
            .Select(d => new DayOfWeekPointDto
            {
                Day = d,
                Name = dayNames[d],
                Count = data.FirstOrDefault(x => x.Day == d)?.Count ?? 0
            })
            .ToList();
    }

    private static List<HourOfDayPointDto> BuildByHourOfDay(IReadOnlyList<SessionRow> sessions)
    {
        var data = sessions
            .GroupBy(s => s.StartedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Select(s => s.ReferenceId).Distinct().Count() })
            .ToList();

        return Enumerable.Range(0, 24)
            .Select(h => new HourOfDayPointDto
            {
                Hour = h,
                Count = data.FirstOrDefault(x => x.Hour == h)?.Count ?? 0
            })
            .ToList();
    }

    private static List<DayOfWeekPointDto> BuildEmptyDayOfWeek()
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        return Enumerable.Range(0, 7)
            .Select(d => new DayOfWeekPointDto { Day = d, Name = dayNames[d], Count = 0 })
            .ToList();
    }

    private static List<HourOfDayPointDto> BuildEmptyHourOfDay() =>
        Enumerable.Range(0, 24)
            .Select(h => new HourOfDayPointDto { Hour = h, Count = 0 })
            .ToList();

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
        TranscodeReason.QualityDownscale => "Quality downscale",
        _ => reason.ToString()
    };

    private sealed record SessionRow(
        Guid MediaId,
        Guid ReferenceId,
        DateTime StartedAt,
        double WatchedDurationSeconds,
        double DurationSeconds,
        Guid? DeviceId,
        string? DeviceName);

    private sealed record MediaInfo(Guid Id, string? Title, MediaType Type);

    private sealed record TrackRelation(
        Guid TrackId,
        Guid? ArtistId,
        string? ArtistTitle,
        Guid? AlbumId,
        string? AlbumTitle);

    private sealed record EpisodeRelation(Guid EpisodeId, Guid SerieId, string? SerieTitle);
}
