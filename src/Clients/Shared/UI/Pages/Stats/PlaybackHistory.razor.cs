using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class PlaybackHistory
{
    private const string FilterStorageKey = "my-space.history";

    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

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

    protected override async Task OnInitializedAsync()
    {
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

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();
}
