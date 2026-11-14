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
using Microsoft.AspNetCore.Components.Web;

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

    // Sections in the same fixed order as MediaCardContextMenu: play, go to, my state, organize, manage.
    private void BuildMenuContent(RenderTreeBuilder builder)
    {
        AddSection(builder, 0, b =>
        {
            var seq = 0;
            AddMenuItem(b, ref seq, "play-circle", L["PlayNext"], PlayNext, primary: true);
            AddMenuItem(b, ref seq, "queue", L["AddToQueue"], AddToQueue);
        });

        var hasRadioActions = _musicIntelligenceAvailable
            || !string.IsNullOrEmpty(Track.Artist)
            || !string.IsNullOrEmpty(Track.Genre);

        if (hasRadioActions)
        {
            AddSection(builder, 10, b =>
            {
                var seq = 0;
                if (_musicIntelligenceAvailable)
                    AddMenuItem(b, ref seq, "waves", string.Format(L["RadioSonic"], Track.Title), RadioSonic);

                if (!string.IsNullOrEmpty(Track.Artist))
                    AddMenuItem(b, ref seq, "radio", string.Format(L["RadioArtist"], Track.Artist), RadioArtist);

                if (!string.IsNullOrEmpty(Track.Genre))
                    AddMenuItem(b, ref seq, "broadcast", string.Format(L["RadioGenre"], Track.Genre), RadioGenre);
            });
        }

        var links = MediaCardRelatedMenu.Visible(
            MediaCardRelatedLinks.Build(albumId: Track.AlbumId, artistId: Track.ArtistId),
            CurrentPath());
        if (links.Count > 0)
        {
            AddSection(builder, 15, b =>
            {
                var seq = 0;
                foreach (var link in links)
                {
                    var href = link.Href;
                    AddMenuItem(
                        b,
                        ref seq,
                        MediaCardRelatedMenu.Icon(link.Kind),
                        L[MediaCardRelatedMenu.LabelKey(link.Kind)],
                        () => NavigationManager.NavigateTo(href));
                }
            });
        }

        if (_canRate)
        {
            AddSection(builder, 20, b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "k7-menu-row");
                b.AddEventStopPropagationAttribute(2, "onclick", true);
                b.AddMarkupContent(3, "<span class=\"k7-menu-item-icon\"><i class=\"ph ph-star k7-icon k7-icon--md\" aria-hidden=\"true\"></i></span>");
                b.OpenElement(4, "span");
                b.AddAttribute(5, "class", "k7-menu-item-text");
                b.AddContent(6, L["MyRating"].Value);
                b.CloseElement();
                b.OpenComponent<RatingStars>(7);
                b.AddAttribute(8, "MediaId", Track.MediaId);
                b.AddAttribute(9, "Value", UserRating);
                b.AddAttribute(10, "Size", "sm");
                b.CloseComponent();
                b.CloseElement();
            });
        }

        var canDownload = DeviceService.GetClientType() != ClientType.Web;
        if (_canCreatePlaylist || canDownload)
        {
            AddSection(builder, 30, b =>
            {
                var seq = 0;
                if (_canCreatePlaylist)
                    AddMenuItem(b, ref seq, "playlist", L["AddToPlaylist"], AddToPlaylist);

                if (canDownload)
                    AddMenuItem(b, ref seq, "download-simple", L["DownloadOffline"], DownloadOffline);
            });
        }

        if (_canEditMetadata)
        {
            AddSection(builder, 50, b =>
            {
                var seq = 0;
                AddMenuItem(b, ref seq, "pencil-simple", L["EditMetadata"], OpenEditMetadataAsync);
            });
        }
    }

    private static void AddSection(RenderTreeBuilder builder, int sequence, RenderFragment content)
    {
        builder.OpenComponent<K7MenuSection>(sequence);
        builder.AddAttribute(sequence + 1, "ChildContent", content);
        builder.CloseComponent();
    }

    private string CurrentPath()
    {
        var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var cut = relative.IndexOfAny(['?', '#']);
        if (cut >= 0)
            relative = relative[..cut];

        return "/" + relative.Trim('/');
    }

    private void AddMenuItem(
        RenderTreeBuilder builder,
        ref int seq,
        string icon,
        string label,
        Func<Task> onClick,
        bool primary = false)
    {
        builder.OpenComponent<K7MenuItem>(seq++);
        builder.AddAttribute(seq++, "Icon", icon);
        builder.AddAttribute(seq++, "Primary", primary);
        builder.AddAttribute(seq++, "OnClick", EventCallback.Factory.Create(this, onClick));
        builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(b => b.AddContent(0, label)));
        builder.CloseComponent();
    }

    private void AddMenuItem(
        RenderTreeBuilder builder,
        ref int seq,
        string icon,
        string label,
        Action onClick,
        bool primary = false)
    {
        builder.OpenComponent<K7MenuItem>(seq++);
        builder.AddAttribute(seq++, "Icon", icon);
        builder.AddAttribute(seq++, "Primary", primary);
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

        await PlayServerRadioAsync(
            MusicRadioType.Genre,
            string.Format(L["RadioGenreSnackbar"], Track.Genre),
            genre: Track.Genre);
    }

    private async Task PlayServerRadioAsync(
        MusicRadioType radioType,
        string radioTitle,
        Guid? seedTrackId = null,
        Guid? seedArtistId = null,
        string? genre = null)
    {
        var started = await MusicRadio.StartAsync(new MusicRadioRequest
        {
            RadioType = radioType.ToString(),
            Title = radioTitle,
            SeedTrackId = seedTrackId,
            SeedArtistId = seedArtistId,
            Genre = genre
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
