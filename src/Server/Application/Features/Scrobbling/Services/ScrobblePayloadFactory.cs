using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Scrobbling.Services;

public class ScrobblePayloadFactory(IApplicationDbContext context)
{
    public async Task<ScrobblePayload?> CreateAsync(
        Guid userId,
        string? userName,
        Guid mediaId,
        PlaybackState state,
        double positionSeconds,
        double durationSeconds,
        bool isCompleted,
        bool isProgressTick = false,
        CancellationToken cancellationToken = default)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null)
            return null;

        var progress = durationSeconds > 0 ? Math.Round(positionSeconds / durationSeconds * 100, 1) : 0d;

        string? artist = null;
        string? album = null;
        string? trackName = null;
        int? trackNumber = null;
        string? showName = null;
        int? seasonNumber = null;
        int? episodeNumber = null;
        string? episodeName = null;
        string? showTmdb = null;
        string? showTvdb = null;
        var tmdb = FindExternal(media.ExternalIds, "tmdb");
        var imdb = FindExternal(media.ExternalIds, "imdb");
        var tvdb = FindExternal(media.ExternalIds, "tvdb");
        var musicBrainz = FindExternal(media.ExternalIds, "musicbrainz");

        if (media.Type == MediaType.MusicTrack)
        {
            var track = await context.Medias
                .OfType<MusicTrack>()
                .AsNoTracking()
                .Include(t => t.Album)
                    .ThenInclude(a => a!.Artist)
                .Include(t => t.Artist)
                .FirstOrDefaultAsync(t => t.Id == mediaId, cancellationToken);

            if (track is not null)
            {
                artist = track.Artist?.Title ?? track.Album?.Artist?.Title;
                album = track.Album?.Title;
                trackName = track.Title;
                trackNumber = track.TrackNumber;
            }
        }
        else if (media.Type == MediaType.SerieEpisode)
        {
            var episode = await context.Medias
                .OfType<SerieEpisode>()
                .AsNoTracking()
                .Include(e => e.Serie)
                    .ThenInclude(s => s.ExternalIds)
                .Include(e => e.Season)
                .FirstOrDefaultAsync(e => e.Id == mediaId, cancellationToken);

            if (episode is not null)
            {
                showName = episode.Serie?.Title;
                seasonNumber = episode.Season?.SeasonNumber;
                episodeNumber = episode.EpisodeNumber;
                episodeName = episode.Title;
                showTmdb = FindExternal(episode.Serie?.ExternalIds, "tmdb");
                showTvdb = FindExternal(episode.Serie?.ExternalIds, "tvdb");
                // Keep episode Tmdb/Tvdb episode-scoped. Show ids go on ShowTmdb/ShowTvdb only
                // (BetaSeries rejects show TVDB as an episode Guid; Yamtrack uses Series.ProviderIds).
            }
        }

        return new ScrobblePayload
        {
            UserId = userId,
            MediaId = mediaId,
            MediaType = media.Type,
            Title = media.Title ?? "",
            OriginalTitle = media.OriginalTitle,
            Year = media.ReleaseDate?.Year,
            Artist = artist,
            Album = album,
            TrackName = trackName,
            TrackNumber = trackNumber,
            ShowName = showName,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            EpisodeName = episodeName,
            Tmdb = tmdb,
            Imdb = imdb,
            Tvdb = tvdb,
            ShowTmdb = showTmdb,
            ShowTvdb = showTvdb,
            MusicBrainz = musicBrainz,
            PositionSeconds = positionSeconds,
            DurationSeconds = durationSeconds,
            ProgressPercent = progress,
            State = state,
            IsCompleted = isCompleted,
            IsProgressTick = isProgressTick,
            Timestamp = DateTimeOffset.UtcNow,
            UserName = userName
        };
    }

    private static string? FindExternal(IEnumerable<ExternalId>? ids, string provider) =>
        ids?.FirstOrDefault(id =>
            id.ProviderName.Contains(provider, StringComparison.OrdinalIgnoreCase))?.Value;
}
