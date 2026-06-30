using ApexCharts;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class WatchStats : IDisposable
{
    private const string FilterStorageKey = "my-space.stats";

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "period")]
    public string? QueryPeriod { get; set; }

    [SupplyParameterFromQuery(Name = "mediaType")]
    public string? QueryMediaType { get; set; }

    [SupplyParameterFromQuery(Name = "type")]
    public string? QueryType { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? QueryTab { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string? QueryFrom { get; set; }

    [SupplyParameterFromQuery(Name = "to")]
    public string? QueryTo { get; set; }

    private WatchStatsDto? _stats;
    private bool _loading = true;
    private string _selectedPeriod = "month";
    private string _selectedMediaType = "";
    private Timer? _debounceTimer;
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);
    private bool _pendingQuerySync;

    private List<ChartDataPoint> _playsOverTimeData = [];
    private List<ChartDataPoint> _hourData = [];
    private List<ChartDataPoint> _dowData = [];
    private List<ChartDataPoint> _genreData = [];
    private List<ChartDataPoint> _deviceData = [];
    private List<ChartDataPoint> _decisionData = [];
    private List<ChartDataPoint> _resolutionData = [];

    private ApexChartOptions<ChartDataPoint> _areaChartOptions = CreateAreaChartOptions();
    private ApexChartOptions<ChartDataPoint> _barChartOptionsHour = CreateBarChartOptions();
    private ApexChartOptions<ChartDataPoint> _barChartOptionsDow = CreateBarChartOptions();
    private ApexChartOptions<ChartDataPoint> _donutChartOptionsGenre = CreateDonutChartOptions();
    private ApexChartOptions<ChartDataPoint> _donutChartOptionsDevice = CreateDonutChartOptions();
    private ApexChartOptions<ChartDataPoint> _donutChartOptionsDecision = CreateDonutChartOptions();
    private ApexChartOptions<ChartDataPoint> _donutChartOptionsResolution = CreateDonutChartOptions();

    private List<ButtonGroupOption<string>> _periodOptions = [];
    private List<ButtonGroupOption<string>> _mediaTypeOptions = [];

    private bool IsMusicOnly => _selectedMediaType == "MusicTrack";
    private bool IsVideoOnly => _selectedMediaType is "Movie" or "SerieEpisode";
    private bool ShowMusicRankings => _selectedMediaType is "" or "MusicTrack";
    private bool ShowShowRankings => _selectedMediaType is "" or "SerieEpisode";

    private string TopItemsLabel => _selectedMediaType switch
    {
        "MusicTrack" => L["TopTracks"],
        "Movie" => L["TopMovies"],
        "SerieEpisode" => L["TopEpisodes"],
        _ => L["TopItems"]
    };

    protected override void OnInitialized()
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
            new("MusicTrack", Label: L["Music"]),
            new("Movie", Label: L["Movies"]),
            new("SerieEpisode", Label: L["TVShows"])
        ];
    }

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.ProgressUpdated += OnProgressUpdated;

        if (PageFilterUrlSync.HasAnyQuery(Navigation, "period", "mediaType", "type", "tab", "from", "to"))
        {
            ApplyFiltersFromQuery();
            await SaveFiltersToStorageAsync();
        }
        else if (await LoadPersistedFiltersAsync())
        {
            _pendingQuerySync = true;
        }

        await FetchStatsAsync();
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(Navigation, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    private void ApplyFiltersFromQuery()
    {
        _selectedPeriod = QueryPeriod ?? PageFilterUrlSync.GetQueryValue(Navigation, "period") ?? "month";
        _selectedMediaType = ResolveMediaTypeFromQuery();

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

    private string ResolveMediaTypeFromQuery()
    {
        var mediaType = QueryMediaType
            ?? PageFilterUrlSync.GetQueryValue(Navigation, "mediaType")
            ?? QueryType
            ?? PageFilterUrlSync.GetQueryValue(Navigation, "type");

        if (!string.IsNullOrEmpty(mediaType))
        {
            return mediaType;
        }

        var tab = QueryTab ?? PageFilterUrlSync.GetQueryValue(Navigation, "tab");
        return tab == "music" ? "MusicTrack" : "";
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["period"] = _selectedPeriod is "month" ? null : _selectedPeriod,
        ["mediaType"] = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType,
        ["type"] = null,
        ["tab"] = null,
        ["from"] = _selectedPeriod == "custom" ? _fromDate.ToString("yyyy-MM-dd") : null,
        ["to"] = _selectedPeriod == "custom" ? _toDate.ToString("yyyy-MM-dd") : null
    };

    private async Task<bool> LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<UserWatchStatsFilterState>(FilterStorageKey, CancellationToken.None);
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
                new UserWatchStatsFilterState(
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

    private void OnProgressUpdated(Guid mediaId, double progress, bool isCompleted)
    {
        if (!isCompleted) return;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await FetchStatsAsync();
                StateHasChanged();
            });
        }, null, 2000, Timeout.Infinite);
    }

    private async Task OnPeriodChanged(string period)
    {
        _selectedPeriod = period ?? "month";
        await PersistFiltersAsync();
        if (_selectedPeriod != "custom")
        {
            await FetchStatsAsync();
        }
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        await PersistFiltersAsync();
        await FetchStatsAsync();
    }

    private async Task OnDateRangeChanged((DateOnly? From, DateOnly? To) range)
    {
        if (range.From is not null) _fromDate = range.From.Value;
        if (range.To is not null) _toDate = range.To.Value;
        await PersistFiltersAsync();
        await FetchStatsAsync();
    }

    private async Task FetchStatsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;
            DateTime? from = null;
            DateTime? to = null;

            if (_selectedPeriod == "custom")
            {
                from = _fromDate.ToDateTime(TimeOnly.MinValue);
                to = _toDate.ToDateTime(TimeOnly.MaxValue);
            }

            _stats = await K7ServerService.GetWatchStatsAsync(mediaTypeParam, _selectedPeriod, from, to);
            BuildChartData();
        }
        catch
        {
            _stats = null;
        }

        _loading = false;
        StateHasChanged();
    }

    private void BuildChartData()
    {
        if (_stats is null) return;

        _playsOverTimeData = _stats.PlaysOverTime
            .Select(p => new ChartDataPoint(p.Date.ToString("MM/dd"), p.Count))
            .ToList();

        _hourData = _stats.PlaysByHourOfDay
            .Select(p => new ChartDataPoint($"{p.Hour}h", p.Count))
            .ToList();

        _dowData = _stats.PlaysByDayOfWeek
            .Select(p => new ChartDataPoint(p.Name[..3], p.Count))
            .ToList();

        _genreData = _stats.TopGenres
            .Take(8)
            .Select(g => new ChartDataPoint(g.Genre, g.PlayCount))
            .ToList();

        _deviceData = _stats.TopDevices
            .Take(6)
            .Select(d => new ChartDataPoint(d.Name, d.PlayCount))
            .ToList();

        if (_stats.PlaybackDetails is { } pd)
        {
            _decisionData = pd.PlaybackDecisions
                .Select(d => new ChartDataPoint(d.Label, d.Count))
                .ToList();

            _resolutionData = pd.TopResolutions
                .Select(r => new ChartDataPoint(r.Label, r.Count))
                .ToList();
        }
        else
        {
            _decisionData = [];
            _resolutionData = [];
        }
    }

    private static ApexChartOptions<ChartDataPoint> CreateAreaChartOptions() => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Sparkline = new ChartSparkline { Enabled = false }
        },
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2 },
        Fill = new Fill { Opacity = new Opacity(0.2) },
        DataLabels = new DataLabels { Enabled = false },
        Xaxis = new XAxis { Labels = new XAxisLabels { RotateAlways = false } },
        Grid = new Grid { Show = false },
        Theme = new Theme { Mode = Mode.Dark }
    };

    private static ApexChartOptions<ChartDataPoint> CreateBarChartOptions() => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false }
        },
        PlotOptions = new PlotOptions
        {
            Bar = new PlotOptionsBar { BorderRadius = 3, ColumnWidth = "60%" }
        },
        DataLabels = new DataLabels { Enabled = false },
        Grid = new Grid { Show = false },
        Yaxis = [new YAxis { Min = 0 }],
        Theme = new Theme { Mode = Mode.Dark }
    };

    private static bool HasNonZeroData(List<ChartDataPoint> data) =>
        data.Count > 0 && data.Any(d => d.Value > 0);

    private static ApexChartOptions<ChartDataPoint> CreateDonutChartOptions() => new()
    {
        Chart = new Chart
        {
            Background = "transparent"
        },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Position = LegendPosition.Bottom },
        Theme = new Theme { Mode = Mode.Dark }
    };

    private static string FormatLanguage(string code)
    {
        var lang = K7.Shared.SupportedLanguages.FindByCode(code);
        return lang?.NativeLabel ?? code.ToUpperInvariant();
    }

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        _debounceTimer?.Dispose();
    }
}
