using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class TrackContextMenu : IDisposable
{
    [Parameter, EditorRequired]
    public required AudioQueueItem Track { get; set; }

    [Parameter]
    public int? UserRating { get; set; }

    [Parameter]
    public EventCallback OnMetadataChanged { get; set; }

    [Inject] private IMusicRadioPlaybackService MusicRadio { get; set; } = default!;

    private readonly Guid _menuOwnerId = Guid.NewGuid();
    private ElementReference _triggerRef;
    private bool _menuOpen;
    private bool _canCreatePlaylist;
    private bool _canRate;
    private bool _canEditMetadata;
    private bool _musicIntelligenceAvailable;
    private bool _capsLoaded;

    protected override void OnInitialized() =>
        ContextMenuService.Changed += OnContextMenuServiceChanged;

    private void OnContextMenuServiceChanged()
    {
        var open = ContextMenuService.Current?.OwnerId == _menuOwnerId;
        if (open == _menuOpen)
            return;

        _menuOpen = open;
        InvokeAsync(StateHasChanged);
    }

    private async Task OpenSharedMenuAsync()
    {
        if (!_capsLoaded)
        {
            var caps = await MediaCardMenuCapabilities.GetAsync(FeatureAccess);
            _canCreatePlaylist = caps.CanCreateLibrary;
            _canRate = caps.CanRate;
            _canEditMetadata = caps.CanEditMetadata;
            _musicIntelligenceAvailable = await MusicIntelligenceAvailabilityCache.GetAsync(ServerPreferences);
            _capsLoaded = true;
        }

        ContextMenuService.Open(new MediaCardContextMenuRequest
        {
            OwnerId = _menuOwnerId,
            Anchor = _triggerRef,
            AnchorKind = MediaCardContextMenuAnchorKind.Activator,
            Title = L["ActionsTitle"],
            Content = BuildMenuContent
        });
    }

    private void BuildMenuContent(RenderTreeBuilder builder)
    {
        var seq = 0;

        if (_canRate)
        {
            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "track-context-rating");
            builder.OpenComponent<RatingStars>(seq++);
            builder.AddAttribute(seq++, "MediaId", Track.MediaId);
            builder.AddAttribute(seq++, "Value", UserRating);
            builder.AddAttribute(seq++, "Size", "sm");
            builder.CloseComponent();
            builder.CloseElement();
        }

        AddMenuItem(builder, ref seq, "play-circle", L["PlayNext"], PlayNext);
        AddMenuItem(builder, ref seq, "queue", L["AddToQueue"], AddToQueue);

        if (_canCreatePlaylist)
        {
            builder.OpenElement(seq++, "hr");
            builder.AddAttribute(seq++, "class", "k7-divider");
            builder.CloseElement();
            AddMenuItem(builder, ref seq, "plus-circle", L["AddToPlaylist"], AddToPlaylist);
        }

        if (DeviceService.GetClientType() != ClientType.Web)
        {
            builder.OpenElement(seq++, "hr");
            builder.AddAttribute(seq++, "class", "k7-divider");
            builder.CloseElement();
            AddMenuItem(builder, ref seq, "download-simple", L["DownloadOffline"], DownloadOffline);
        }

        var hasRadioActions = _musicIntelligenceAvailable
            || !string.IsNullOrEmpty(Track.Artist)
            || !string.IsNullOrEmpty(Track.Genre);

        if (hasRadioActions)
        {
            builder.OpenElement(seq++, "hr");
            builder.AddAttribute(seq++, "class", "k7-divider");
            builder.CloseElement();

            if (_musicIntelligenceAvailable)
                AddMenuItem(builder, ref seq, "waves", string.Format(L["RadioSonic"], Track.Title), RadioSonic);

            if (!string.IsNullOrEmpty(Track.Artist))
                AddMenuItem(builder, ref seq, "radio", string.Format(L["RadioArtist"], Track.Artist), RadioArtist);

            if (!string.IsNullOrEmpty(Track.Genre))
                AddMenuItem(builder, ref seq, "broadcast", string.Format(L["RadioGenre"], Track.Genre), RadioGenre);
        }

        if (_canEditMetadata)
        {
            builder.OpenElement(seq++, "hr");
            builder.AddAttribute(seq++, "class", "k7-divider");
            builder.CloseElement();
            AddMenuItem(builder, ref seq, "pencil-simple", L["EditMetadata"], OpenEditMetadataAsync);
        }
    }

    private void AddMenuItem(
        RenderTreeBuilder builder,
        ref int seq,
        string icon,
        string label,
        Func<Task> onClick)
    {
        builder.OpenComponent<K7MenuItem>(seq++);
        builder.AddAttribute(seq++, "Icon", icon);
        builder.AddAttribute(seq++, "OnClick", EventCallback.Factory.Create(this, onClick));
        builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    }

    private void AddMenuItem(
        RenderTreeBuilder builder,
        ref int seq,
        string icon,
        string label,
        Action onClick)
    {
        builder.OpenComponent<K7MenuItem>(seq++);
        builder.AddAttribute(seq++, "Icon", icon);
        builder.AddAttribute(seq++, "OnClick", EventCallback.Factory.Create(this, onClick));
        builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    }

    private void PlayNext()
    {
        Audio.AddToQueueNext(Track);
        Snackbar.Add(string.Format(L["PlayNextSnackbar"], Track.Title), K7Severity.Info);
    }

    private void AddToQueue()
    {
        Audio.AddToQueue(Track);
        Snackbar.Add(string.Format(L["AddedToQueueSnackbar"], Track.Title), K7Severity.Info);
    }

    private async Task AddToPlaylist()
    {
        var parameters = new K7DialogParameters<Dialogs.AddToPlaylistDialog>
        {
            { x => x.MediaId, Track.MediaId },
            { x => x.SourceMediaType, MediaType.MusicTrack }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<Dialogs.AddToPlaylistDialog>(L["AddToPlaylistTitle"], parameters, options);
    }

    private async Task RadioSonic()
    {
        await PlayServerRadioAsync(
            MusicRadioType.Sonic,
            string.Format(L["RadioSonicSnackbar"], Track.Title),
            seedTrackId: Track.MediaId);
    }

    private async Task RadioArtist()
    {
        if (Track.ArtistId is null)
            return;

        await PlayServerRadioAsync(
            MusicRadioType.Artist,
            string.Format(L["RadioArtistSnackbar"], Track.Artist),
            seedArtistId: Track.ArtistId);
    }

    private async Task RadioGenre()
    {
        if (string.IsNullOrEmpty(Track.Genre))
            return;

        var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            Genres = [Track.Genre],
            PageNumber = 1,
            PageSize = 200
        });

        var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Select(ToQueueItem)
            .ToList();

        if (tracks is { Count: > 0 })
        {
            if (!Audio.Shuffle)
                Audio.ToggleShuffle();

            await Audio.PlayRadioAsync(tracks, string.Format(L["RadioGenreSnackbar"], Track.Genre), Random.Shared.Next(tracks.Count));
        }
    }

    private async Task PlayServerRadioAsync(
        MusicRadioType radioType,
        string radioTitle,
        Guid? seedTrackId = null,
        Guid? seedArtistId = null)
    {
        var started = await MusicRadio.StartAsync(new MusicRadioRequest
        {
            RadioType = radioType.ToString(),
            Title = radioTitle,
            SeedTrackId = seedTrackId,
            SeedArtistId = seedArtistId
        });

        if (!started)
        {
            Snackbar.Add(L["RadioEmpty"], K7Severity.Warning);
            return;
        }

        Snackbar.Add(radioTitle, K7Severity.Info);
    }

    private AudioQueueItem ToQueueItem(LiteMusicTrackDto t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title ?? S["Untitled"],
        Artist = t.ArtistName,
        AlbumTitle = t.AlbumTitle,
        ArtistId = t.ArtistId,
        Genre = t.Genre,
        CoverUrl = ApiClient.GetAbsoluteUri(
            (t.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? t.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        Duration = t.Duration
    };

    private async Task OpenEditMetadataAsync()
    {
        ContextMenuService.Close();
        try
        {
            var media = await K7ServerService.GetMediaAsync(Track.MediaId);
            if (media is not MusicTrackDto track)
            {
                Snackbar.Add(L["EditMetadataError"], K7Severity.Error);
                return;
            }

            var parameters = new K7DialogParameters<Dialogs.EditMetadataDialog>
            {
                { x => x.Media, track }
            };
            var options = new K7DialogOptions
            {
                CloseOnEscapeKey = true,
                MaxWidth = K7DialogMaxWidth.Medium,
                FullWidth = true
            };
            var dialog = await DialogService.ShowAsync<Dialogs.EditMetadataDialog>(L["EditMetadata"], parameters, options);
            var result = await dialog.Result;
            if (result is { Canceled: false } && OnMetadataChanged.HasDelegate)
                await OnMetadataChanged.InvokeAsync();
        }
        catch
        {
            Snackbar.Add(L["EditMetadataError"], K7Severity.Error);
        }
    }

    private async Task DownloadOffline()
    {
        await DownloadManager.EnqueueAsync(new DownloadRequest
        {
            IndexedFileId = Track.IndexedFileId,
            MediaId = Track.MediaId,
            Title = Track.Title,
            Artist = Track.Artist,
            AlbumTitle = Track.AlbumTitle,
            CoverUrl = Track.CoverUrl,
            MediaType = MediaType.MusicTrack,
            IsCacheItem = false
        });
        Snackbar.Add(string.Format(L["DownloadQueued"], Track.Title), K7Severity.Info);
    }

    public void Dispose()
    {
        ContextMenuService.Changed -= OnContextMenuServiceChanged;
        if (_menuOpen)
            ContextMenuService.Close();
    }
}
