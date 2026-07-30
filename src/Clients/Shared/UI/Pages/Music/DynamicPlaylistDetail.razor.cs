using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class DynamicPlaylistDetail
{
    [Parameter] public required string Id { get; set; }
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private DynamicPlaylistDto? _dynamicPlaylist;
    private List<DynamicPlaylistItemViewModel> _items = [];
    private IReadOnlyList<string> _headerPreviewUrls = [];
    private string? _rulesDescription;
    private bool _loading = true;
    private bool _loadingItems = true;
    private bool _evaluating;

    private bool _showHeaderPlaceholder => _items.Count == 0;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _dynamicPlaylist = await K7ServerService.GetDynamicPlaylistAsync(Guid.Parse(Id));
        if (_dynamicPlaylist is not null)
        {
            _rulesDescription = BuildRulesDescription(_dynamicPlaylist);
            await LoadItemsAsync();
        }
        _loading = false;
    }

    private async Task LoadItemsAsync()
    {
        _loadingItems = true;
        var playlistId = Guid.Parse(Id);
        var result = await K7ServerService.GetPlaylistItemsAsync(playlistId, 1, 200);
        _items = result?.Items?.Select(ToViewModel).ToList() ?? [];
        _headerPreviewUrls = _items
            .Select(i => i.CoverUrl)
            .Where(url => !string.IsNullOrEmpty(url))
            .Cast<string>()
            .Take(4)
            .ToList();
        _loadingItems = false;
    }

    private DynamicPlaylistItemViewModel ToViewModel(PlaylistItemDto item) => new()
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
        IsPlaying = Audio.CurrentTrack?.MediaId == item.MediaId
    };

    private async Task RecordPlaybackAsync()
    {
        try
        {
            await K7ServerService.RecordPlaylistPlaybackAsync(Guid.Parse(Id));
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
            await Audio.PlayTracksAsync(queue, 0, Guid.Parse(Id));
        }
    }

    private async Task ShuffleAll()
    {
        var queue = BuildQueueItems();
        if (queue.Count > 0)
        {
            await RecordPlaybackAsync();
            await Audio.PlayShuffledAsync(queue, Guid.Parse(Id));
        }
    }

    private async Task OnTrackClick(K7.Clients.Shared.UI.Components.TableRowClickEventArgs<DynamicPlaylistItemViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;
        var queue = BuildQueueItems();
        var index = queue.FindIndex(q => q.MediaId == track.MediaId);
        await RecordPlaybackAsync();
        await Audio.PlayTracksAsync(queue, index >= 0 ? index : 0, Guid.Parse(Id));
    }

    private List<AudioQueueItem> BuildQueueItems() =>
        _items
            .Where(i => i.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();

    private static AudioQueueItem BuildQueueItem(DynamicPlaylistItemViewModel i) => new()
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
        UserRating = i.UserRating
    };

    private async Task EvaluateAsync()
    {
        _evaluating = true;
        StateHasChanged();
        try
        {
            await K7ServerService.EvaluateDynamicPlaylistAsync(Guid.Parse(Id));
            _dynamicPlaylist = await K7ServerService.GetDynamicPlaylistAsync(Guid.Parse(Id));
            await LoadItemsAsync();
            Snackbar.Add(L["ReevaluateSuccess"], K7Severity.Success);
        }
        catch { Snackbar.Add(L["ReevaluateError"], K7Severity.Error); }
        finally
        {
            _evaluating = false;
            StateHasChanged();
        }
    }

    private async Task BrowseInLibraryAsync()
    {
        if (_dynamicPlaylist is null || _dynamicPlaylist.RuleFilter.Items.Count == 0)
            return;

        try
        {
            var groups = await LibraryService.GetLibraryGroupsAsync();
            var libraryMediaType = LibraryGroupBrowseNavigationHelper.ToLibraryMediaType(_dynamicPlaylist.MediaType);
            var groupId = LibraryGroupBrowseNavigationHelper.ResolveGroupId(groups, null, libraryMediaType);
            if (groupId is null)
            {
                Snackbar.Add(L["BrowseInLibraryUnavailable"], K7Severity.Info);
                return;
            }

            var url = LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(
                groupId.Value,
                new LibraryGroupBrowseUrlState(
                    MediaType: _dynamicPlaylist.MediaType,
                    Filter: _dynamicPlaylist.RuleFilter));

            NavigationManager.NavigateTo(url);
        }
        catch
        {
            Snackbar.Add(L["BrowseInLibraryUnavailable"], K7Severity.Error);
        }
    }

    private async Task OpenEditDialog()
    {
        if (_dynamicPlaylist is null) return;
        var parameters = new K7DialogParameters<DynamicPlaylistDialog>
        {
            { x => x.DynamicPlaylistId, _dynamicPlaylist.Id },
            { x => x.InitialTitle, _dynamicPlaylist.Title },
            { x => x.InitialDescription, _dynamicPlaylist.Description },
            { x => x.InitialMediaType, _dynamicPlaylist.MediaType },
            { x => x.InitialRuleFilter, _dynamicPlaylist.RuleFilter },
            { x => x.InitialLimit, _dynamicPlaylist.Limit },
            { x => x.InitialOrderBy, _dynamicPlaylist.OrderBy },
            { x => x.InitialOrderDescending, _dynamicPlaylist.OrderDescending }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<DynamicPlaylistDialog>(L["EditDialogTitle"], parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await EvaluateAsync();
        }
    }

    private async Task ConfirmDelete()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteDialogTitle"],
            $"{S["Delete"]} \u00ab {_dynamicPlaylist?.Title} \u00bb ?",
            yesText: S["Delete"], cancelText: S["Cancel"]);
        if (result == true)
        {
            try
            {
                await K7ServerService.DeleteDynamicPlaylistAsync(Guid.Parse(Id));
                Snackbar.Add(L["DeleteSuccess"], K7Severity.Success);
                NavigationManager.NavigateTo("/my-space/playlists");
            }
            catch { Snackbar.Add(L["DeleteError"], K7Severity.Error); }
        }
    }

    private string BuildRulesDescription(DynamicPlaylistDto sp)
    {
        var items = sp.RuleFilter.Items.OfType<ConditionRuleItemDto>().ToList();
        if (items.Count == 0) return L["NoRules"];

        var condition = sp.RuleFilter.MatchCondition == RuleMatchCondition.All
            ? L["MatchAnd"].Value
            : L["MatchOr"].Value;
        var rules = items.Select(r =>
        {
            var field = RuleFieldLocalization.GetFieldLabel(r.Field, FieldL, FieldL);
            var op = GetOperatorLabel(r.Operator);
            return string.IsNullOrEmpty(r.Value)
                ? $"{field} {op}"
                : string.Format(L["RuleWithValue"], field, op, r.Value);
        });

        var desc = string.Join(condition, rules);
        if (sp.Limit.HasValue)
            desc += string.Format(L["LimitSuffix"], sp.Limit);
        return desc;
    }

    private string GetOperatorLabel(RuleOperator op) => op switch
    {
        RuleOperator.Equals => OpL["OpEquals"],
        RuleOperator.NotEquals => OpL["OpNotEquals"],
        RuleOperator.Contains => OpL["OpContains"],
        RuleOperator.NotContains => OpL["OpNotContains"],
        RuleOperator.GreaterThan => OpL["OpGreaterThan"],
        RuleOperator.LessThan => OpL["OpLessThan"],
        RuleOperator.GreaterThanOrEqual => OpL["OpGreaterThanOrEqual"],
        RuleOperator.LessThanOrEqual => OpL["OpLessThanOrEqual"],
        RuleOperator.BeginsWith => OpL["OpBeginsWith"],
        RuleOperator.EndsWith => OpL["OpEndsWith"],
        RuleOperator.InLast => OpL["OpInLast"],
        RuleOperator.IsEmpty => OpL["OpIsEmpty"],
        RuleOperator.IsNotEmpty => OpL["OpIsNotEmpty"],
        _ => op.ToString()
    };

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private string FormatRelativeTime(DateTimeOffset dateTime)
    {
        var diff = DateTimeOffset.UtcNow - dateTime;
        if (diff.TotalMinutes < 1) return L["RelativeJustNow"];
        if (diff.TotalMinutes < 60) return string.Format(L["RelativeMinutes"], (int)diff.TotalMinutes);
        if (diff.TotalHours < 24) return string.Format(L["RelativeHours"], (int)diff.TotalHours);
        return string.Format(L["RelativeDays"], (int)diff.TotalDays);
    }

    internal sealed record DynamicPlaylistItemViewModel
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
        public bool IsPlaying { get; init; }
    }
}
