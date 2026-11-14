using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaCardContextMenu
{
    [Parameter, EditorRequired]
    public MediaCardViewModel Model { get; set; } = default!;

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public bool ShowPlay { get; set; }

    [Parameter]
    public bool ShowReview { get; set; }

    [Parameter]
    public bool ShowRating { get; set; }

    [Parameter]
    public bool ShowPlaylist { get; set; }

    [Parameter]
    public bool ShowCollection { get; set; }

    [Parameter]
    public bool ShowWatchState { get; set; }

    [Parameter]
    public bool ExcludeMenuEnabled { get; set; }

    [Parameter]
    public bool ContinueWatchingMenuEnabled { get; set; }

    [Parameter]
    public bool IsAdmin { get; set; }

    [Parameter]
    public int? BulkEpisodeCount { get; set; }

    [Parameter]
    public EventCallback OnExcludeForSelf { get; set; }

    [Parameter]
    public EventCallback OnExcludeForOthers { get; set; }

    [Parameter]
    public EventCallback OnDismissFromContinueWatching { get; set; }

    [Parameter]
    public EventCallback OnWatchStateChanged { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private ISyncPlayMediaLoader MediaLoader { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> SharedStrings { get; set; } = default!;
    [Inject] private IStringLocalizer<MediaReviewDialog> ReviewDialogL { get; set; } = default!;

    private Guid _mediaId;
    private bool _hasValidMediaId;
    private IReadOnlyList<MediaCardRelatedLink> _relatedLinks = [];

    private MediaType? MenuMediaType => MediaCardMenuActions.InferMediaType(Model);

    /// <summary>A movie, an episode or a track: one file that can play right away.</summary>
    private bool IsSingleMedia =>
        MenuMediaType is MediaType.Movie or MediaType.SerieEpisode or MediaType.MusicTrack;

    /// <summary>
    /// The main action starts playback: a single media, a series or a season (the episode to
    /// watch next) or an album (all its tracks). Anything else only opens its page.
    /// </summary>
    private bool IsPlayable =>
        _hasValidMediaId
        && (IsSingleMedia || MenuMediaType is MediaType.Serie or MediaType.SerieSeason or MediaType.MusicAlbum);

    private bool IsInProgress =>
        _hasValidMediaId && MenuMediaType is not MediaType.MusicAlbum && Model.Progress > 0 && !Model.Watched;

    /// <summary>Started and not finished: the main action resumes, and a second one starts over.</summary>
    private bool CanResume => IsSingleMedia && IsInProgress;

    /// <summary>Says what the main action really does: play, resume, or open the page.</summary>
    private string PlayLabel =>
        !IsPlayable ? L["Open"]
        : MenuMediaType is MediaType.MusicAlbum ? L["PlayAlbum"]
        : IsInProgress ? L["Resume"]
        : L["Play"];

    private bool HasStateSection =>
        _hasValidMediaId && (ShowRating || ShowReview || ShowWatchState || ContinueWatchingMenuEnabled);

    protected override void OnParametersSet()
    {
        _hasValidMediaId = Model?.Id is not null && Guid.TryParse(Model.Id, out _mediaId);
        _relatedLinks = MediaCardRelatedMenu.Visible(Model?.RelatedLinks, Href);
    }

    private void Navigate(string href)
    {
        if (!string.IsNullOrEmpty(href))
            NavigationManager.NavigateTo(href);
    }

    /// <summary>
    /// Starts playback. A movie, an episode or a track plays right away, from where it was left.
    /// A series or a season plays its episode to watch next, an album plays all its tracks.
    /// When nothing can play from here (an artist, a title only available from another server),
    /// the page opens instead.
    /// </summary>
    private async Task OnPlayAsync(bool fromBeginning = false)
    {
        if (!IsPlayable)
        {
            Navigate(Href ?? "");
            return;
        }

        try
        {
            var started = MenuMediaType switch
            {
                MediaType.Serie => await PlaySerieAsync(),
                MediaType.SerieSeason => await PlaySeasonAsync(),
                MediaType.MusicAlbum => await PlayAlbumAsync(),
                _ => await PlayMediaAsync(_mediaId, Model.Title, fromBeginning)
            };

            if (!started)
                Navigate(Href ?? "");
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(SharedStrings["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task<bool> PlayMediaAsync(Guid mediaId, string? title, bool fromBeginning = false)
    {
        var media = await MediaService.GetMediaAsync(mediaId);
        if (media?.IndexedFiles is not { Count: > 0 })
            return false;

        var startPosition = !fromBeginning && media.UserState is { IsCompleted: false, LastPlaybackPosition: > 1 } state
            ? state.LastPlaybackPosition
            : (double?)null;

        await MediaLoader.LoadAndPlayMediaAsync(mediaId, title, Model.PictureUrl, startPosition);
        return true;
    }

    /// <summary>The episode in progress, else the next one to watch: the same choice as the series page.</summary>
    private async Task<bool> PlaySerieAsync()
    {
        var episode = await MediaService.GetEpisodeToPlayAsync(_mediaId);
        return episode is not null && await PlayMediaAsync(episode.Id, title: null);
    }

    private async Task<bool> PlaySeasonAsync()
    {
        if (await MediaService.GetMediaAsync(_mediaId) is not SerieSeasonDto season)
            return false;

        var episodes = (season.Episodes ?? [])
            .Where(e => e.IndexedFileId.HasValue)
            .OrderBy(e => e.EpisodeNumber)
            .ToList();
        var episode = episodes.FirstOrDefault(e => e.UserState?.IsCompleted != true) ?? episodes.FirstOrDefault();

        return episode is not null && await PlayMediaAsync(episode.Id, title: null);
    }

    private async Task<bool> PlayAlbumAsync()
    {
        if (await MediaService.GetMediaAsync(_mediaId) is not MusicAlbumDto album)
            return false;

        var tracks = (album.Tracks ?? [])
            .Where(t => t.IndexedFileId.HasValue || t.RemoteIndexedFileId.HasValue)
            .OrderBy(t => t.DiscNumber is null or < 1 ? 1 : t.DiscNumber.Value)
            .ThenBy(t => t.TrackNumber)
            .Select(t => new AudioQueueItem
            {
                IndexedFileId = t.IndexedFileId ?? Guid.Empty,
                RemoteIndexedFileId = t.RemoteIndexedFileId,
                MediaId = t.Id,
                Title = t.Title ?? Model.Title ?? "",
                Artist = t.ArtistName ?? album.ArtistName,
                ArtistId = t.ArtistId ?? album.ArtistId,
                AlbumId = album.Id,
                AlbumTitle = album.Title,
                CoverUrl = Model.PictureUrl,
                Duration = t.Duration
            })
            .ToList();
        if (tracks.Count == 0)
            return false;

        await Audio.PlayTracksAsync(tracks, 0, sourceKind: MusicSessionSourceKind.Album, sourceId: album.Id);
        return true;
    }

    private async Task ToggleWatchStateAsync()
    {
        var watched = !Model.Watched;
        var scope = WatchStateActions.GetScope(Model.Kind);
        var success = await WatchStateActions.ApplyAsync(
            MediaService,
            CacheStore,
            DialogService,
            Snackbar,
            SharedStrings,
            _mediaId,
            watched,
            scope,
            BulkEpisodeCount);

        if (!success)
            return;

        WatchStateActions.ApplyLocalCardState(Model, watched);
        if (OnWatchStateChanged.HasDelegate)
            await OnWatchStateChanged.InvokeAsync();

        await InvokeAsync(StateHasChanged);
    }

    private async Task AddToPlaylistAsync()
    {
        var parameters = new K7DialogParameters<AddToPlaylistDialog>
        {
            { x => x.MediaId, _mediaId },
            { x => x.SourceMediaType, MediaCardMenuActions.InferMediaType(Model) }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AddToPlaylistDialog>(L["AddToPlaylistTitle"], parameters, options);
    }

    private async Task AddToCollectionAsync()
    {
        var mediaType = MediaCardMenuActions.InferMediaType(Model);
        var parameters = new K7DialogParameters<AddToCollectionDialog>
        {
            { x => x.MediaId, _mediaId },
            { x => x.MediaType, mediaType }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AddToCollectionDialog>(L["AddToCollectionTitle"], parameters, options);
    }

    private async Task OpenReviewDialogAsync()
    {
        await MediaReviewDialogHelper.OpenAsync(DialogService, ReviewDialogL, _mediaId, Model.Title);
    }
}
