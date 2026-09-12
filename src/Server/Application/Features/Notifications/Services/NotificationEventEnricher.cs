using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Notifications.Services;

public class NotificationEventEnricher(
    IApplicationDbContext context,
    INotificationServerInfo serverInfo)
{
    public async Task<IReadOnlyDictionary<string, object?>> EnrichAsync(
        BaseEvent domainEvent,
        IReadOnlyDictionary<string, object?> eventData,
        CancellationToken cancellationToken = default)
    {
        var enriched = EnrichWithGlobals(eventData);
        ApplyTemplateAliases(domainEvent, enriched);

        var mediaId = TryGetMediaId(enriched);
        if (mediaId is Guid id)
            await AddMediaFieldsAsync(enriched, id, cancellationToken);

        return enriched;
    }

    public Dictionary<string, object?> EnrichWithGlobals(IReadOnlyDictionary<string, object?> eventData)
    {
        var now = DateTime.UtcNow;
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.Local);
        var enriched = new Dictionary<string, object?>(eventData, StringComparer.OrdinalIgnoreCase)
        {
            ["Server.Name"] = serverInfo.Name,
            ["Server.Url"] = serverInfo.Url,
            ["Server.Version"] = serverInfo.Version,
            ["Current.Year"] = local.Year,
            ["Current.Month"] = local.Month,
            ["Current.Day"] = local.Day,
            ["Current.Hour"] = local.Hour,
            ["Current.Minute"] = local.Minute,
            ["Current.Weekday"] = local.DayOfWeek.ToString(),
            ["Current.Datestamp"] = local.ToString("yyyy-MM-dd"),
            ["Current.Timestamp"] = now.ToString("o"),
            ["Current.UnixTime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        return enriched;
    }

    public static Dictionary<string, object?> SamplesFrom(
        IEnumerable<NotificationParameterInfo> parameters,
        INotificationServerInfo? serverInfo = null)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in parameters)
            data[param.Name] = param.SampleValue;

        if (serverInfo is not null)
        {
            data["Server.Name"] = string.IsNullOrWhiteSpace(serverInfo.Name) ? "K7" : serverInfo.Name;
            data["Server.Url"] = string.IsNullOrWhiteSpace(serverInfo.Url) ? "https://k7.example.com" : serverInfo.Url;
            data["Server.Version"] = string.IsNullOrWhiteSpace(serverInfo.Version) ? "1.0.0" : serverInfo.Version;
        }

        return data;
    }

    private static void ApplyTemplateAliases(BaseEvent domainEvent, Dictionary<string, object?> data)
    {
        switch (domainEvent)
        {
            case PlaybackStateChangedEvent playback:
                data["User.Name"] = playback.UserName;
                data["User.Id"] = playback.UserId.ToString();
                data["Media.Title"] = playback.MediaTitle;
                data["Media.Type"] = playback.MediaType;
                data["State"] = FormatPlaybackState(playback.State);
                data["PreviousState"] = FormatPlaybackState(playback.PreviousState);
                data["Session.State"] = FormatPlaybackState(playback.State);
                data["Session.PreviousState"] = FormatPlaybackState(playback.PreviousState);
                data["Session.Position"] = playback.Position;
                data["Session.Duration"] = playback.Duration;
                data["Session.ProgressPercent"] = playback.Duration > 0
                    ? Math.Round(playback.Position / playback.Duration * 100, 1)
                    : 0d;
                break;
            case UserCreatedEvent created:
                ApplyUserAliases(data, created.UserId, created.UserName, created.Email, created.Role);
                data["User.Origin"] = created.Origin.ToString();
                data["Origin"] = created.Origin.ToString();
                break;
            case UserDeletedEvent deleted:
                ApplyUserAliases(data, deleted.UserId, deleted.UserName, deleted.Email, deleted.Role);
                break;
            case ApiKeyCreatedEvent apiKeyCreated:
                ApplyApiKeyAliases(data, apiKeyCreated.Name, apiKeyCreated.Scope, apiKeyCreated.KeyPrefix);
                ApplyUserAliases(data, apiKeyCreated.CreatedByUserId, apiKeyCreated.CreatedByUserName, null, null);
                break;
            case ApiKeyRevokedEvent apiKeyRevoked:
                ApplyApiKeyAliases(data, apiKeyRevoked.Name, apiKeyRevoked.Scope, apiKeyRevoked.KeyPrefix);
                ApplyUserAliases(data, apiKeyRevoked.CreatedByUserId, apiKeyRevoked.CreatedByUserName, null, null);
                break;
            case ClientAppPasswordCreatedEvent appPasswordCreated:
                data["ClientAppPassword.Name"] = appPasswordCreated.Name;
                ApplyUserAliases(data, appPasswordCreated.UserId, appPasswordCreated.UserName, null, null);
                break;
            case ClientAppPasswordRevokedEvent appPasswordRevoked:
                data["ClientAppPassword.Name"] = appPasswordRevoked.Name;
                ApplyUserAliases(data, appPasswordRevoked.UserId, appPasswordRevoked.UserName, null, null);
                break;
            case MediaRatedEvent rated:
                ApplyUserAliases(data, rated.UserId, rated.UserName, null, null);
                data["Rating.Value"] = rated.RatingValue;
                data["Rating.IsNew"] = rated.IsNew;
                break;
            case MediaReviewUpsertedEvent reviewUpserted:
                ApplyUserAliases(data, reviewUpserted.UserId, reviewUpserted.UserName, null, null);
                data["Rating.Value"] = reviewUpserted.RatingValue;
                data["Review.Text"] = reviewUpserted.Text;
                data["Review.Emoji"] = reviewUpserted.Emoji;
                data["Review.IsNew"] = reviewUpserted.IsNew;
                break;
            case MediaReviewDeletedEvent reviewDeleted:
                ApplyUserAliases(data, reviewDeleted.UserId, reviewDeleted.UserName, null, null);
                break;
            case MediaHiddenChangedEvent hidden:
                ApplyUserAliases(data, hidden.UserId, hidden.UserName, null, null);
                data["Hidden.IsHidden"] = hidden.IsHidden;
                data["Hidden.IsSelfExcluded"] = hidden.IsSelfExcluded;
                data["Hidden.IsAdminExcluded"] = hidden.IsAdminExcluded;
                break;
        }
    }

    private static void ApplyUserAliases(
        Dictionary<string, object?> data,
        Guid userId,
        string? userName,
        string? email,
        string? role)
    {
        data["User.Id"] = userId.ToString();
        data["UserId"] = userId.ToString();
        if (userName is not null)
            data["User.Name"] = userName;
        if (email is not null)
            data["User.Email"] = email;
        if (role is not null)
            data["User.Role"] = role;
    }

    private static void ApplyApiKeyAliases(
        Dictionary<string, object?> data,
        string name,
        string scope,
        string keyPrefix)
    {
        data["ApiKey.Name"] = name;
        data["ApiKey.Scope"] = scope;
        data["ApiKey.KeyPrefix"] = keyPrefix;
    }

    private static string FormatPlaybackState(PlaybackState state) =>
        state == PlaybackState.Idle ? "Stopped" : state.ToString();

    private static Guid? TryGetMediaId(IReadOnlyDictionary<string, object?> data)
    {
        foreach (var key in new[] { "Media.Id", "Session.MediaId", "MediaId" })
        {
            if (data.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id))
                return id;
        }

        return null;
    }

    private async Task AddMediaFieldsAsync(
        Dictionary<string, object?> data,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.ExternalIds)
            .Include(m => m.MetadataTags)
                .ThenInclude(t => t.MetadataTag)
            .Include(m => m.Pictures)
            .Include(m => ((MusicTrack)m).Album)
                .ThenInclude(a => a!.Artist)
            .Include(m => ((MusicTrack)m).Artist)
            .Include(m => ((MusicAlbum)m).Artist)
            .Include(m => ((SerieEpisode)m).Serie)
            .Include(m => ((SerieEpisode)m).Season)
            .Include(m => ((SerieSeason)m).Serie)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null)
            return;

        data["Media.Title"] = media.Title;
        data["Media.OriginalTitle"] = media.OriginalTitle;
        data["Media.Type"] = media.Type.ToString();
        data["Media.ReleaseDate"] = media.ReleaseDate?.ToString("yyyy-MM-dd");
        data["Media.Year"] = media.ReleaseDate?.Year;
        data["Media.Genres"] = string.Join(", ",
            media.MetadataTags
                .Where(t => t.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(t => t.MetadataTag.DisplayName));
        data["Media.Url"] = BuildMediaUrl(media);

        SetExternal(data, media, "tmdb", "External.Tmdb");
        SetExternal(data, media, "imdb", "External.Imdb");
        SetExternal(data, media, "tvdb", "External.Tvdb");
        SetExternal(data, media, "musicbrainz", "External.MusicBrainz");

        var pictureType = media.Type is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist
            ? MetadataPictureType.Cover
            : MetadataPictureType.Poster;
        var picture = media.Pictures.FirstOrDefault(p => p.Type == pictureType)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Cover);
        if (picture is not null)
            data["PictureUrl"] = $"/api/metadata-pictures/{picture.Id}?size=Small";

        var backdrop = media.Pictures.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
        if (backdrop is not null)
            data["BackdropUrl"] = $"/api/metadata-pictures/{backdrop.Id}?size=Medium";

        switch (media)
        {
            case SerieEpisode episode:
                data["Show.Name"] = episode.Serie?.Title;
                data["Season.Number"] = episode.Season?.SeasonNumber;
                data["Episode.Number"] = episode.EpisodeNumber;
                data["Episode.Name"] = episode.Title;
                break;
            case SerieSeason season:
                data["Show.Name"] = season.Serie?.Title;
                data["Season.Number"] = season.SeasonNumber;
                break;
            case Serie serie:
                data["Show.Name"] = serie.Title;
                break;
            case MusicTrack track:
                data["Track.Name"] = track.Title;
                data["Track.Number"] = track.TrackNumber;
                data["Album.Name"] = track.Album?.Title;
                data["Artist.Name"] = track.Artist?.Title ?? track.Album?.Artist?.Title;
                break;
            case MusicAlbum album:
                data["Album.Name"] = album.Title;
                data["Artist.Name"] = album.Artist?.Title;
                break;
            case MusicArtist artist:
                data["Artist.Name"] = artist.Title;
                break;
        }
    }

    private string? BuildMediaUrl(BaseMedia media)
    {
        var baseUrl = serverInfo.Url.TrimEnd('/');
        return media switch
        {
            Movie => $"{baseUrl}/movies/{media.Id}",
            Serie => $"{baseUrl}/series/{media.Id}",
            SerieSeason season => $"{baseUrl}/series/{season.SerieId}/seasons/{season.SeasonNumber}",
            SerieEpisode episode => episode.Season is not null
                ? $"{baseUrl}/series/{episode.SerieId}/seasons/{episode.Season.SeasonNumber}/episodes/{episode.EpisodeNumber}"
                : $"{baseUrl}/series/{episode.SerieId}",
            MusicAlbum => $"{baseUrl}/music/albums/{media.Id}",
            MusicArtist => $"{baseUrl}/music/artists/{media.Id}",
            MusicTrack track => $"{baseUrl}/music/albums/{track.AlbumId}",
            _ => $"{baseUrl}/"
        };
    }

    private static void SetExternal(Dictionary<string, object?> data, BaseMedia media, string provider, string key)
    {
        var value = media.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, provider, StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(value))
            data[key] = value;
    }
}
