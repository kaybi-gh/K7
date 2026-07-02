using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class PlaylistDetail
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    private PlaylistDto? _playlist;
    private List<PlaylistItemViewModel> _items = [];
    private List<PlaylistBrowseRow> _browseRows = [];
    private IReadOnlyList<string> _headerPreviewUrls = [];
    private double _totalDuration;
    private bool _loading = true;
    private bool _loadingItems = true;
    private bool _canTrackProgress;
    private bool _canSetWatchState;
    private BrowseView<PlaylistBrowseRow>? _browseView;
    private K7DataTable<PlaylistItemViewModel>? _dataTable;
    private string? _activeSortKey = "order";
    private K7SortDirection _activeSortDirection = K7SortDirection.Ascending;

    private bool _isMusicPlaylist => _playlist?.MediaType is MediaType.MusicTrack;
    private bool _showHeaderPlaceholder => _items.Count == 0;

    internal sealed record PlaylistBrowseRow(PlaylistItemViewModel Item, int Index)
    {
        public bool IsAlternate => Index % 2 == 1;
    }

    protected override async Task OnParametersSetAsync()
    {
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        _loading = true;
        _playlist = await K7ServerService.GetPlaylistAsync(Guid.Parse(Id));

        if (_playlist is not null)
        {
            await LoadItemsAsync();
        }

        _loading = false;
    }

    private async Task LoadItemsAsync()
    {
        _loadingItems = true;
        var result = await K7ServerService.GetPlaylistItemsAsync(Guid.Parse(Id), 1, 200);

        _items = result?.Items?
            .Select(ToViewModel)
            .ToList() ?? [];

        RebuildBrowseRows();
        _totalDuration = _items.Sum(i => i.Duration);
        _headerPreviewUrls = _items
            .Select(i => i.CoverUrl)
            .Where(url => !string.IsNullOrEmpty(url))
            .Cast<string>()
            .Take(4)
            .ToList();
        _loadingItems = false;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();
    }

    private Task<K7DataTableResult<PlaylistItemViewModel>> LoadTableDataAsync(
        K7DataTableState<PlaylistItemViewModel> state, CancellationToken cancellationToken)
    {
        if (state.Count <= 0)
            return Task.FromResult(new K7DataTableResult<PlaylistItemViewModel>([], 0));

        var sorted = SortPlaylistItems(_items, state.SortKey, state.SortDirection);
        var items = sorted
            .Skip(state.StartIndex)
            .Take(state.Count)
            .ToList();

        return Task.FromResult(new K7DataTableResult<PlaylistItemViewModel>(items, sorted.Count));
    }

    private static List<PlaylistItemViewModel> SortPlaylistItems(
        IReadOnlyList<PlaylistItemViewModel> items,
        string? sortKey,
        K7SortDirection direction)
    {
        var desc = direction is K7SortDirection.Descending;
        IEnumerable<PlaylistItemViewModel> query = items;

        query = sortKey switch
        {
            "title" => desc ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            "artist" => desc ? query.OrderByDescending(i => i.ArtistName) : query.OrderBy(i => i.ArtistName),
            "duration" => desc ? query.OrderByDescending(i => i.Duration) : query.OrderBy(i => i.Duration),
            _ => desc ? query.OrderByDescending(i => i.Order) : query.OrderBy(i => i.Order)
        };

        return query.ToList();
    }

    private async Task OnTableSortChanged(SortChangedEventArgs args)
    {
        _activeSortKey = args.SortKey;
        _activeSortDirection = args.Direction;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();
    }

    private void OnColumnPickerRequested() =>
        _dataTable?.ToggleColumnPicker();

    private void RebuildBrowseRows()
    {
        _browseRows = _items
            .Select((item, index) => new PlaylistBrowseRow(item, index))
            .ToList();
    }

    private PlaylistItemViewModel ToViewModel(PlaylistItemDto item) => new()
    {
        Id = item.Id,
        MediaId = item.MediaId,
        Order = item.Order,
        Title = item.MediaTitle ?? S["Untitled"],
        ArtistName = item.ArtistName,
        ArtistId = item.ArtistId,
        AlbumTitle = item.AlbumTitle,
        Genre = item.Genre,
        IndexedFileId = item.IndexedFileId,
        CoverUrl = ApiClient.GetAbsoluteUri(
            (item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still))?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        CoverDominantColor = (item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still))?.DominantColor,
        Duration = item.Duration ?? 0,
        UserRating = item.UserRating,
        IsPlaying = Audio.CurrentTrack?.MediaId == item.MediaId
    };

    private MediaCardViewModel? GetCardViewModel(PlaylistItemViewModel item) =>
        _playlist?.MediaType switch
        {
            MediaType.Movie => new MediaCardViewModel
            {
                Id = item.MediaId.ToString(),
                Kind = MediaCardKind.Poster,
                MediaType = MediaType.Movie,
                Title = item.Title,
                PictureUrl = item.CoverUrl,
                UserRating = item.UserRating
            },
            MediaType.SerieEpisode => new MediaCardViewModel
            {
                Id = item.MediaId.ToString(),
                Kind = MediaCardKind.Episode,
                MediaType = MediaType.SerieEpisode,
                Title = item.Title,
                PictureUrl = item.CoverUrl,
                UserRating = item.UserRating
            },
            _ => null
        };

    private string? GetItemHref(PlaylistItemViewModel item) =>
        _playlist?.MediaType switch
        {
            MediaType.Movie => $"/movies/{item.MediaId}",
            _ => null
        };

    private Guid PlaylistId => Guid.Parse(Id);

    private async Task RecordPlaybackAsync()
    {
        try
        {
            await K7ServerService.RecordPlaylistPlaybackAsync(PlaylistId);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PlayAll()
    {
        var queue = BuildQueueItems();
        if (queue.Count > 0)
        {
            await RecordPlaybackAsync();
            await Audio.PlayTracksAsync(queue, 0, PlaylistId);
        }
    }

    private async Task ShuffleAll()
    {
        var queue = BuildQueueItems();
        if (queue.Count > 0)
        {
            if (!Audio.Shuffle) Audio.ToggleShuffle();
            await RecordPlaybackAsync();
            await Audio.PlayTracksAsync(queue, 0, PlaylistId);
        }
    }

    private async Task OnItemActivated(PlaylistItemViewModel item)
    {
        if (_isMusicPlaylist)
        {
            var queue = BuildQueueItems();
            var index = queue.FindIndex(q => q.MediaId == item.MediaId);
            await RecordPlaybackAsync();
            await Audio.PlayTracksAsync(queue, index >= 0 ? index : 0, PlaylistId);
            return;
        }

        await RecordPlaybackAsync();
        var href = GetItemHref(item);
        if (href is not null)
            NavigationManager.NavigateTo(href);
    }

    private async Task OnListKeyDown(KeyboardEventArgs e, PlaylistItemViewModel item)
    {
        if (e.Key is "Enter" or " ")
            await OnItemActivated(item);
    }

    private List<AudioQueueItem> BuildQueueItems()
    {
        return _items
            .Where(i => i.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();
    }

    private static AudioQueueItem BuildQueueItem(PlaylistItemViewModel i) => new()
    {
        IndexedFileId = i.IndexedFileId!.Value,
        MediaId = i.MediaId,
        Title = i.Title,
        Artist = i.ArtistName,
        ArtistId = i.ArtistId,
        AlbumTitle = i.AlbumTitle,
        Genre = i.Genre,
        CoverUrl = i.CoverUrl,
        CoverDominantColor = i.CoverDominantColor,
        Duration = i.Duration,
        UserRating = i.UserRating,
        Bpm = i.Bpm,
        MusicalKey = i.MusicalKey,
        Energy = i.Energy
    };

    private async Task RemoveItem(PlaylistItemViewModel item)
    {
        try
        {
            await K7ServerService.RemovePlaylistItemAsync(Guid.Parse(Id), item.Id);
            _items.Remove(item);
            RebuildBrowseRows();
            _totalDuration = _items.Sum(i => i.Duration);
            if (_playlist is not null)
                _playlist = _playlist with { ItemCount = _items.Count };
            _headerPreviewUrls = _items
                .Select(i => i.CoverUrl)
                .Where(url => !string.IsNullOrEmpty(url))
                .Cast<string>()
                .Take(4)
                .ToList();

            if (_dataTable is not null)
                await _dataTable.RefreshAsync();

            StateHasChanged();
        }
        catch
        {
            Snackbar.Add(L["DeleteError"], K7Severity.Error);
        }
    }

    private async Task OpenEditDialog()
    {
        if (_playlist is null) return;

        var parameters = new K7DialogParameters<EditPlaylistDialog>
        {
            { x => x.PlaylistId, _playlist.Id },
            { x => x.Title, _playlist.Title },
            { x => x.Description, _playlist.Description },
            { x => x.MediaType, _playlist.MediaType },
            { x => x.CoverPictureId, _playlist.CoverPicture?.Id }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EditPlaylistDialog>(L["EditDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            _playlist = await K7ServerService.GetPlaylistAsync(Guid.Parse(Id));
            await LoadItemsAsync();
            StateHasChanged();
        }
    }

    private async Task ConfirmDelete()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteDialogTitle"],
            $"{S["Delete"]} \u00ab {_playlist?.Title} \u00bb ?",
            yesText: S["Delete"], cancelText: S["Cancel"]);

        if (result == true)
        {
            try
            {
                await K7ServerService.DeletePlaylistAsync(Guid.Parse(Id));
                Snackbar.Add(L["DeleteSuccess"], K7Severity.Success);
                NavigationManager.NavigateTo("/playlists");
            }
            catch
            {
                Snackbar.Add(L["DeleteError"], K7Severity.Error);
            }
        }
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private static string FormatTotalDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours} h {ts.Minutes:00} min";
        return $"{ts.Minutes} min";
    }

    private string GetItemLabel(MediaType mediaType) => mediaType switch
    {
        MediaType.MusicTrack => S["Tracks"],
        MediaType.Movie => L["Movies"],
        MediaType.SerieEpisode => L["Episodes"],
        _ => L["Items"]
    };

    private IReadOnlyList<DownloadRequest> GetDownloadRequests()
    {
        return _items
            .Where(i => i.IndexedFileId.HasValue)
            .Select(i => new DownloadRequest
            {
                IndexedFileId = i.IndexedFileId!.Value,
                MediaId = i.MediaId,
                Title = i.Title,
                Artist = i.ArtistName,
                AlbumTitle = i.AlbumTitle,
                CoverUrl = i.CoverUrl,
                MediaType = _playlist?.MediaType ?? MediaType.MusicTrack,
                IsCacheItem = false
            })
            .ToList();
    }

    internal sealed record PlaylistItemViewModel
    {
        public Guid Id { get; init; }
        public Guid MediaId { get; init; }
        public int Order { get; init; }
        public required string Title { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistId { get; init; }
        public string? AlbumTitle { get; init; }
        public string? Genre { get; init; }
        public Guid? IndexedFileId { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public double Duration { get; init; }
        public int? UserRating { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public bool IsPlaying { get; init; }
    }
}
