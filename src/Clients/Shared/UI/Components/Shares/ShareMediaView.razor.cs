using System.Globalization;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Shares;

public partial class ShareMediaView
{
    [Inject] private IPublicShareService PublicShare { get; set; } = default!;
    [Inject] private IK7ServerService ServerService { get; set; } = default!;

    /// <summary>Media to show, as returned for the share (already limited to its scope).</summary>
    [Parameter, EditorRequired] public MediaDto Media { get; set; } = default!;

    /// <summary>The visitor's session: which share, and whether it may play or download.</summary>
    [Parameter, EditorRequired] public OpenShareResultDto Session { get; set; } = default!;

    /// <summary>Link back to the root of the share. Null on the root itself.</summary>
    [Parameter] public string? BackHref { get; set; }

    /// <summary>Raised when the visitor starts a movie or an episode.</summary>
    [Parameter] public EventCallback<ShareVideoPlayRequest> OnPlayVideo { get; set; }

    /// <summary>Raised when the visitor starts tracks. The queue only holds tracks of this page.</summary>
    [Parameter] public EventCallback<ShareAudioPlayRequest> OnPlayAudio { get; set; }

    private ElementReference _scrollRoot;

    private Guid ShareId => Session.ShareId ?? Guid.Empty;

    private bool IsAudio => Media is MusicTrackDto or MusicAlbumDto or MusicArtistDto;

    /// <summary>A movie, an episode or a track: it plays on its own page.</summary>
    private bool IsSingleItem => Media is MovieDto or SerieEpisodeDto or MusicTrackDto;

    /// <summary>An album or an artist: the main button plays everything on the page.</summary>
    private bool IsContainer => Media is MusicAlbumDto or MusicArtistDto;

    private IndexedFileDto? PrimaryFile => IsSingleItem ? Media.IndexedFiles?.FirstOrDefault() : null;

    private IReadOnlyList<IndexedFileDto> OtherFiles =>
        IsSingleItem && Media.IndexedFiles is { Count: > 1 } files ? files.Skip(1).ToList() : [];

    private bool CanPlayHero => Session.AllowPlayback && Media switch
    {
        MusicAlbumDto or MusicArtistDto => BuildQueue().Count > 0,
        MovieDto or SerieEpisodeDto or MusicTrackDto => PrimaryFile is not null,
        _ => false
    };

    private bool HasBelowContent =>
        OtherFiles.Count > 0
        || Cast.Count > 0
        || Media is SerieDto { Seasons.Count: > 0 }
            or SerieSeasonDto { Episodes.Count: > 0 }
            or MusicArtistDto { Albums.Count: > 0 }
            or MusicAlbumDto { Tracks.Count: > 0 };

    private string DisplayTitle => Media switch
    {
        SerieEpisodeDto episode => GetEpisodeTitle(episode.Title, episode.EpisodeNumber),
        SerieSeasonDto season => GetSeasonTitle(season.Title, season.SeasonNumber),
        _ => Media.Title ?? ""
    };

    /// <summary>
    /// A logo replaces the title of a movie or a series. Seasons and episodes show the series
    /// artwork: their own title goes under it.
    /// </summary>
    private bool ShowTitleUnderLogo => Media is SerieSeasonDto or SerieEpisodeDto;

    /// <summary>What the media belongs to. Shown as plain text: parents are not part of the share.</summary>
    private string? Context => Media switch
    {
        SerieEpisodeDto episode => JoinParts(
            episode.SerieTitle,
            $"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}"),
        SerieSeasonDto season => season.SerieTitle,
        MusicAlbumDto album => album.ArtistName,
        MusicTrackDto track => JoinParts(track.ArtistName, track.AlbumTitle),
        _ => null
    };

    private string? Overview => Media switch
    {
        MovieDto movie => movie.Overview,
        SerieDto serie => serie.Overview,
        SerieSeasonDto season => season.Overview,
        SerieEpisodeDto episode => episode.Overview,
        MusicAlbumDto album => album.Overview,
        MusicArtistDto artist => artist.Biography,
        _ => null
    };

    private double? ExternalRating =>
        Media.Ratings?.FirstOrDefault(rating => rating.Source == RatingSource.MetadataProvider)?.Value;

    private string? ContentRating => Media switch
    {
        MovieDto { ContentRating: { Length: > 0 } rating } => rating,
        SerieDto { ContentRating: { Length: > 0 } rating } => rating,
        _ => null
    };

    private List<string> Facts
    {
        get
        {
            var facts = new List<string>();
            var date = Media is SerieEpisodeDto episode ? episode.AirDate ?? Media.ReleaseDate : Media.ReleaseDate;
            if (date is { } releaseDate)
                facts.Add(releaseDate.Year.ToString(CultureInfo.InvariantCulture));

            if (PrimaryFile?.FileMetadata is { } metadata && GetDuration(metadata) is { } duration)
                facts.Add(FormatDuration(duration.TotalSeconds));

            if (Media is MusicAlbumDto { Tracks: { Count: > 0 } tracks })
                facts.Add(string.Format(L["TrackCount"], tracks.Count));

            return facts;
        }
    }

    private IReadOnlyList<PersonRoleDisplayHelper.GroupedDisplay> Cast =>
        PersonRoleDisplayHelper.GroupForCarousel(Media.PersonRoles);

    private MetadataPictureDto? BackdropPicture =>
        Media.Pictures?.FirstOrDefault(picture => picture.Type == MetadataPictureType.Backdrop)
        ?? (Media is SerieEpisodeDto
            ? Media.Pictures?.FirstOrDefault(picture => picture.Type == MetadataPictureType.Still)
            : null);

    private string? DominantColor => BackdropPicture?.DominantColor ?? CoverPicture?.DominantColor;

    private string? BackdropUrl =>
        MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(BackdropPicture, ServerService, null).Item1;

    private string? BackdropHighResUrl =>
        MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(BackdropPicture, ServerService, null).Item2;

    private string? LogoUrl => GetPictureUrl(
        Media.Pictures?.FirstOrDefault(picture => picture.Type == MetadataPictureType.Logo),
        MetadataPictureSize.Medium);

    /// <summary>Music shows its cover in the hero. Video shows a logo or its title.</summary>
    private MetadataPictureDto? CoverPicture => IsAudio
        ? Media.Pictures?.FirstOrDefault(picture => picture.Type == MetadataPictureType.Cover)
            ?? Media.Pictures?.FirstOrDefault(picture => picture.Type is MetadataPictureType.Poster or MetadataPictureType.Portrait)
        : null;

    private string? CoverUrl => GetPictureUrl(CoverPicture, MetadataPictureSize.Medium);

    private string? GetPictureUrl(MetadataPictureDto? picture, MetadataPictureSize? size) =>
        picture?.Uri is null
            ? null
            : ServerService.GetAbsoluteUri(picture.GetUri(size)?.OriginalString)?.AbsoluteUri;

    private string GetMediaHref(Guid mediaId) => SharePaths.Media(ShareId, mediaId);

    private string GetDownloadUrl(Guid indexedFileId) => PublicShare.GetDownloadUrl(ShareId, indexedFileId);

    private Task PlayHeroAsync() => Media switch
    {
        MusicTrackDto or MusicAlbumDto or MusicArtistDto => PlayQueueAsync(0),
        _ => PlayVideoAsync(Media.Id, null)
    };

    private Task PlayVideoAsync(Guid mediaId, Guid? indexedFileId) =>
        OnPlayVideo.InvokeAsync(new ShareVideoPlayRequest(mediaId, indexedFileId));

    private Task PlayTrackAsync(Guid trackId)
    {
        var index = BuildQueue().FindIndex(item => item.MediaId == trackId);
        return PlayQueueAsync(Math.Max(0, index));
    }

    private Task PlayQueueAsync(int startIndex)
    {
        var queue = BuildQueue();
        return queue.Count == 0
            ? Task.CompletedTask
            : OnPlayAudio.InvokeAsync(new ShareAudioPlayRequest(queue, startIndex));
    }

    /// <summary>
    /// Everything on this page that can be listened to, in order. Nothing from outside the
    /// share ever enters the queue: no radio, no similar tracks.
    /// </summary>
    private List<AudioQueueItem> BuildQueue() => Media switch
    {
        MusicTrackDto track when PrimaryFile is { } file =>
        [
            new AudioQueueItem
            {
                IndexedFileId = file.Id,
                MediaId = track.Id,
                Title = track.Title ?? "",
                Artist = track.ArtistName,
                AlbumTitle = track.AlbumTitle,
                CoverUrl = CoverUrl,
                Duration = file.FileMetadata is AudioFileMetadataDto audio ? audio.Duration.TotalSeconds : null
            }
        ],
        MusicAlbumDto album => ToQueue(album, CoverUrl),
        MusicArtistDto artist => (artist.Albums ?? [])
            .OrderByDescending(album => album.ReleaseDate)
            .SelectMany(album => ToQueue(
                album,
                GetPictureUrl(
                    album.Pictures?.FirstOrDefault(picture => picture.Type == MetadataPictureType.Cover),
                    MetadataPictureSize.Medium)))
            .ToList(),
        _ => []
    };

    private static List<AudioQueueItem> ToQueue(MusicAlbumDto album, string? coverUrl) =>
        (album.Tracks ?? [])
            .Where(track => track.IndexedFileId is not null)
            .OrderBy(track => track.DiscNumber ?? 1)
            .ThenBy(track => track.TrackNumber ?? 0)
            .Select(track => new AudioQueueItem
            {
                IndexedFileId = track.IndexedFileId!.Value,
                MediaId = track.Id,
                Title = track.Title ?? "",
                Artist = track.ArtistName ?? album.ArtistName,
                AlbumTitle = album.Title,
                CoverUrl = coverUrl,
                Duration = track.Duration
            })
            .ToList();

    private string GetSeasonTitle(string? title, int seasonNumber) =>
        string.IsNullOrWhiteSpace(title) ? string.Format(L["SeasonNumber"], seasonNumber) : title;

    private string GetEpisodeTitle(string? title, int episodeNumber) =>
        string.IsNullOrWhiteSpace(title) ? string.Format(L["EpisodeNumber"], episodeNumber) : title;

    private string GetAlbumSubtitle(MusicAlbumDto album)
    {
        var parts = new List<string>();
        if (album.ReleaseDate is { } releaseDate)
            parts.Add(releaseDate.Year.ToString(CultureInfo.InvariantCulture));

        if (album.Tracks is { Count: > 0 } tracks)
            parts.Add(string.Format(L["TrackCount"], tracks.Count));

        return string.Join(" - ", parts);
    }

    private static TimeSpan? GetDuration(FileMetadataDto metadata) => metadata switch
    {
        VideoFileMetadataDto video => video.Duration,
        AudioFileMetadataDto audio => audio.Duration,
        _ => null
    };

    private static string? JoinParts(params string?[] parts)
    {
        var kept = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return kept.Count > 0 ? string.Join(" - ", kept) : null;
    }

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = Math.Max(0, bytes);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{size:0.#} {units[unit]}");
    }
}
