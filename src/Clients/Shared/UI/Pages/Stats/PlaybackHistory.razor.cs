using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class PlaybackHistory : IAsyncDisposable
{
    private const string FilterStorageKey = "my-space.history";
    private const int SelectAllPageSize = 100;

    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "period")]
    public string? QueryPeriod { get; set; }

    [SupplyParameterFromQuery(Name = "mediaType")]
    public string? QueryMediaType { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string? QueryFrom { get; set; }

    [SupplyParameterFromQuery(Name = "to")]
    public string? QueryTo { get; set; }

    private K7DataTable<PlaybackHistoryItemDto>? _tableRef;
    private string _selectedMediaType = "";
    private string _selectedPeriod = "month";
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);
    private const int PageSize = 50;
    private int _tableKey;
    private int _totalCount;
    private List<ButtonGroupOption<string>> _mediaTypeOptions = [];
    private List<ButtonGroupOption<string>> _periodOptions = [];
    private bool _pendingQuerySync;
    private bool _selectionMode;
    private bool _busy;
    private readonly HashSet<Guid> _selectedIds = [];
    private readonly Dictionary<Guid, PlaybackHistoryItemDto> _loadedItems = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    private int SelectedCount => _selectedIds.Count;

    private IEnumerable<PlaybackHistoryItemDto> SelectableItems =>
        _loadedItems.Values.Where(CanSelect);

    private bool HasSelectableItems => SelectableItems.Any();

    private bool AllSelected
    {
        get
        {
            var selectable = SelectableItems.ToList();
            return selectable.Count > 0 && selectable.All(item => _selectedIds.Contains(item.ReferenceId));
        }
    }

    private int SelectedReassignableCount =>
        _selectedIds.Count(id => _loadedItems.TryGetValue(id, out var item) && item.CanReassign);

    private int SelectedDeletableCount =>
        _selectedIds.Count(id => _loadedItems.TryGetValue(id, out var item) && item.CanDelete);

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAllAsync));

        _periodOptions =
        [
            new("week", Label: L["WeekShort"]),
            new("month", Label: L["MonthShort"]),
            new("year", Label: L["YearShort"]),
            new("all", Label: L["AllTime"]),
            new("custom", Label: L["CustomShort"])
        ];

        _mediaTypeOptions =
        [
            new("", Label: L["All"]),
            new("3", Label: L["Music"]),
            new("1", Label: L["Movies"]),
            new("5", Label: L["TVShows"])
        ];

        if (PageFilterUrlSync.HasAnyQuery(Navigation, "period", "mediaType", "from", "to"))
        {
            ApplyFiltersFromQuery();
            await SaveFiltersToStorageAsync();
            _tableKey++;
        }
        else if (await LoadPersistedFiltersAsync())
        {
            _tableKey++;
            _pendingQuerySync = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_selectionKeys is not null)
            await _selectionKeys.DisposeAsync();
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(Navigation, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    private void ApplyFiltersFromQuery()
    {
        _selectedPeriod = QueryPeriod ?? PageFilterUrlSync.GetQueryValue(Navigation, "period") ?? "month";
        _selectedMediaType = QueryMediaType ?? PageFilterUrlSync.GetQueryValue(Navigation, "mediaType") ?? "";

        var from = QueryFrom ?? PageFilterUrlSync.GetQueryValue(Navigation, "from");
        var to = QueryTo ?? PageFilterUrlSync.GetQueryValue(Navigation, "to");
        if (DateOnly.TryParse(from, out var fromDate))
        {
            _fromDate = fromDate;
        }

        if (DateOnly.TryParse(to, out var toDate))
        {
            _toDate = toDate;
        }
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["period"] = _selectedPeriod is "month" ? null : _selectedPeriod,
        ["mediaType"] = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType,
        ["from"] = _selectedPeriod == "custom" ? _fromDate.ToString("yyyy-MM-dd") : null,
        ["to"] = _selectedPeriod == "custom" ? _toDate.ToString("yyyy-MM-dd") : null
    };

    private async Task<bool> LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<UserPlaybackHistoryFilterState>(FilterStorageKey, CancellationToken.None);
            if (state is null)
            {
                return false;
            }

            _selectedMediaType = state.MediaType ?? "";
            _selectedPeriod = string.IsNullOrWhiteSpace(state.Period) ? "month" : state.Period;
            if (DateOnly.TryParse(state.From, out var from))
            {
                _fromDate = from;
            }

            if (DateOnly.TryParse(state.To, out var to))
            {
                _toDate = to;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SaveFiltersToStorageAsync()
    {
        try
        {
            await PageFilterStorage.SaveAsync(
                FilterStorageKey,
                new UserPlaybackHistoryFilterState(
                    _selectedMediaType,
                    _selectedPeriod,
                    _selectedPeriod == "custom" ? _fromDate.ToString("yyyy-MM-dd") : null,
                    _selectedPeriod == "custom" ? _toDate.ToString("yyyy-MM-dd") : null),
                CancellationToken.None);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PersistFiltersAsync()
    {
        await SaveFiltersToStorageAsync();
        SyncFiltersToQuery();
    }

    private async Task OnPeriodChanged(string period)
    {
        _selectedPeriod = period ?? "month";
        await PersistFiltersAsync();
        if (_selectedPeriod != "custom")
        {
            await RefreshTableAsync();
        }
    }

    private async Task OnDateRangeChanged((DateOnly? From, DateOnly? To) range)
    {
        if (range.From is not null) _fromDate = range.From.Value;
        if (range.To is not null) _toDate = range.To.Value;
        await PersistFiltersAsync();
        await RefreshTableAsync();
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        await PersistFiltersAsync();
        await RefreshTableAsync();
    }

    private Task RefreshTableAsync()
    {
        ExitSelectionMode();
        _loadedItems.Clear();
        _tableKey++;
        return InvokeAsync(StateHasChanged);
    }

    private async Task<K7DataTableResult<PlaybackHistoryItemDto>> LoadServerDataAsync(
        K7DataTableState<PlaybackHistoryItemDto> state, CancellationToken cancellationToken)
    {
        var startIndex = state.StartIndex;
        var count = state.Count;
        if (count <= 0) return new K7DataTableResult<PlaybackHistoryItemDto>([], 0);

        var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;
        DateTime? from = _selectedPeriod == "custom" ? _fromDate.ToDateTime(TimeOnly.MinValue) : null;
        DateTime? to = _selectedPeriod == "custom" ? _toDate.ToDateTime(TimeOnly.MaxValue) : null;

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => K7ServerService.GetPlaybackHistoryAsync(page, PageSize, mediaTypeParam, _selectedPeriod, from, to, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<PlaybackHistoryItemDto>(count);
            foreach (var result in results)
            {
                if (result is null)
                {
                    continue;
                }

                totalCount = Math.Max(totalCount, result.TotalCount);
                if (result.Items is { Count: > 0 })
                {
                    allItems.AddRange(result.Items);
                    foreach (var item in result.Items)
                        _loadedItems[item.ReferenceId] = item;
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();
            _totalCount = totalCount;
            await InvokeAsync(StateHasChanged);

            return new K7DataTableResult<PlaybackHistoryItemDto>(items, totalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new K7DataTableResult<PlaybackHistoryItemDto>([], 0);
        }
    }

    private async Task ReassignAsync(PlaybackHistoryItemDto item)
    {
        await ShowReassignDialogAsync([item], item.SharedProfileId, item.MediaTitle ?? S["Untitled"]);
    }

    private async Task ReassignSelectedAsync()
    {
        var items = SelectedItems().Where(i => i.CanReassign).ToList();
        if (items.Count == 0 || _busy)
            return;

        var currentProfileId = items.Select(i => i.SharedProfileId).Distinct().Count() == 1
            ? items[0].SharedProfileId
            : null;
        await ShowReassignDialogAsync(items, currentProfileId, items[0].MediaTitle ?? S["Untitled"]);
    }

    private async Task ShowReassignDialogAsync(
        IReadOnlyList<PlaybackHistoryItemDto> items,
        Guid? currentSharedProfileId,
        string title)
    {
        var referenceIds = items.Select(i => i.ReferenceId).ToList();
        var parameters = new K7DialogParameters<ReassignPlaybackHistoryDialog>
        {
            { x => x.ReferenceId, referenceIds[0] },
            { x => x.ReferenceIds, referenceIds },
            { x => x.CurrentSharedProfileId, currentSharedProfileId },
            { x => x.MediaTitle, title }
        };
        var options = new K7DialogOptions
        {
            MaxWidth = K7DialogMaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        var dialogTitle = items.Count == 1 ? L["ReassignTitle"] : L["ReassignSelectedTitle"];
        var dialog = await DialogService.ShowAsync<ReassignPlaybackHistoryDialog>(dialogTitle, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            Snackbar.Add(
                items.Count == 1 ? L["Reassigned"] : string.Format(L["ReassignSelectedSuccess"], items.Count),
                K7Severity.Success);
            await RefreshTableAsync();
        }
    }

    private async Task DeleteAsync(PlaybackHistoryItemDto item)
    {
        var title = item.MediaTitle ?? S["Untitled"];
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteTitle"],
            string.Format(item.SharedProfileId is null ? L["DeleteConfirm"] : L["DeleteSharedConfirm"], title),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (confirmed != true)
            return;

        try
        {
            await K7ServerService.DeletePlaybackHistoryAsync(item.ReferenceId);
            Snackbar.Add(item.SharedProfileId is null ? L["Deleted"] : L["DeletedShared"], K7Severity.Success);
            await RefreshTableAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var items = SelectedItems().Where(i => i.CanDelete).ToList();
        if (items.Count == 0 || _busy)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteSelectedTitle"],
            string.Format(L["DeleteSelectedConfirm"], items.Count),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (confirmed != true)
            return;

        _busy = true;
        var failed = 0;
        try
        {
            foreach (var item in items)
            {
                try
                {
                    await K7ServerService.DeletePlaybackHistoryAsync(item.ReferenceId);
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            _busy = false;
        }

        await RefreshTableAsync();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], items.Count), K7Severity.Success);
        else if (failed == items.Count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], items.Count - failed, failed), K7Severity.Warning);
    }

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
        _tableRef?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(true);
    }

    private void ExitSelectionMode()
    {
        if (!_selectionMode)
            return;

        _selectionMode = false;
        _selectedIds.Clear();
        _tableRef?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(false);
    }

    private void ToggleSelection(PlaybackHistoryItemDto item)
    {
        if (!CanSelect(item) || _busy)
            return;

        if (!_selectedIds.Remove(item.ReferenceId))
            _selectedIds.Add(item.ReferenceId);

        _tableRef?.Rerender();
    }

    private async Task ToggleSelectAllAsync()
    {
        if (_busy)
            return;

        if (AllSelected)
        {
            _selectedIds.Clear();
            _tableRef?.Rerender();
            return;
        }

        await SelectAllLoadedAsync();
    }

    private async Task SelectAllLoadedAsync()
    {
        if (_loadedItems.Count < _totalCount)
            await LoadAllForSelectionAsync();

        _selectedIds.Clear();
        foreach (var item in SelectableItems)
            _selectedIds.Add(item.ReferenceId);

        _tableRef?.Rerender();
    }

    private async Task LoadAllForSelectionAsync()
    {
        _busy = true;
        try
        {
            var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;
            DateTime? from = _selectedPeriod == "custom" ? _fromDate.ToDateTime(TimeOnly.MinValue) : null;
            DateTime? to = _selectedPeriod == "custom" ? _toDate.ToDateTime(TimeOnly.MaxValue) : null;
            var page = 1;
            while (true)
            {
                var result = await K7ServerService.GetPlaybackHistoryAsync(
                    page,
                    SelectAllPageSize,
                    mediaTypeParam,
                    _selectedPeriod,
                    from,
                    to);
                if (result?.Items is not { Count: > 0 })
                    break;

                foreach (var item in result.Items)
                    _loadedItems[item.ReferenceId] = item;

                if (_loadedItems.Count >= result.TotalCount || result.Items.Count < SelectAllPageSize)
                    break;

                page++;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void OnSelectionEscape()
    {
        if (_busy)
            return;

        ExitSelectionMode();
    }

    private Task OnSelectionSelectAllAsync()
    {
        if (!_selectionMode || _busy)
            return Task.CompletedTask;

        return SelectAllLoadedAsync();
    }

    private void OnRowClick(PlaybackHistoryItemDto item)
    {
        if (_selectionMode)
            ToggleSelection(item);
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

    private static bool CanSelect(PlaybackHistoryItemDto item) => item.CanReassign || item.CanDelete;

    private string? GetRowClass(PlaybackHistoryItemDto item) =>
        _selectionMode && IsSelected(item.ReferenceId) ? "is-selected" : null;

    private IEnumerable<PlaybackHistoryItemDto> SelectedItems()
    {
        foreach (var id in _selectedIds)
        {
            if (_loadedItems.TryGetValue(id, out var item))
                yield return item;
        }
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();
}
