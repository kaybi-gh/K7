using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

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

    private PlaylistDto? _playlist;
    private List<PlaylistItemViewModel> _items = [];
    private string? _coverUrl;
    private double _totalDuration;
    private bool _loading = true;
    private bool _loadingItems = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _playlist = await K7ServerService.GetPlaylistAsync(Guid.Parse(Id));

        if (_playlist is not null)
        {
            _coverUrl = ApiClient.GetAbsoluteUri(
                _playlist.CoverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

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

        _totalDuration = _items.Sum(i => i.Duration);
        _loadingItems = false;
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
                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        CoverDominantColor = (item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?.DominantColor,
        Duration = item.Duration ?? 0,
        UserRating = item.UserRating,
        Bpm = item.Bpm,
        MusicalKey = item.MusicalKey,
        Energy = item.Energy,
        IsPlaying = Audio.CurrentTrack?.MediaId == item.MediaId
    };

    private async Task PlayAll()
    {
        var queue = BuildQueueItems();
        if (queue.Count > 0)
            await Audio.PlayTracksAsync(queue, 0);
    }

    private async Task ShuffleAll()
    {
        var queue = BuildQueueItems();
        if (queue.Count > 0)
        {
            if (!Audio.Shuffle) Audio.ToggleShuffle();
            await Audio.PlayTracksAsync(queue, 0);
        }
    }

    private async Task OnTrackClick(K7.Clients.Shared.UI.Components.TableRowClickEventArgs<PlaylistItemViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queue = BuildQueueItems();
        var index = queue.FindIndex(q => q.MediaId == track.MediaId);
        await Audio.PlayTracksAsync(queue, index >= 0 ? index : 0);
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
            _totalDuration = _items.Sum(i => i.Duration);
            if (_playlist is not null)
                _playlist = _playlist with { ItemCount = _items.Count };
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
            { x => x.Description, _playlist.Description }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7.Clients.Shared.UI.Components.Dialogs.EditPlaylistDialog>(L["EditDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            _playlist = await K7ServerService.GetPlaylistAsync(Guid.Parse(Id));
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
