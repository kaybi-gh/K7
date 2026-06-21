using ApexCharts;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class WatchStats : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "type")]
    public string? Type { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? Tab { get; set; }

    private WatchStatsDto? _stats;
    private bool _loading = true;
    private string _selectedPeriod = "month";
    private string _selectedMediaType = "";
    private Timer? _debounceTimer;
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);

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

        _selectedMediaType = ResolveInitialMediaType();
    }

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.ProgressUpdated += OnProgressUpdated;
        await FetchStatsAsync();
    }

    private string ResolveInitialMediaType()
    {
        if (Tab == "music")
            return "MusicTrack";

        return Type switch
        {
            "MusicTrack" or "Movie" or "SerieEpisode" => Type,
            _ => ""
        };
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
        if (_selectedPeriod != "custom")
            await FetchStatsAsync();
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        UpdateMediaTypeUrl(_selectedMediaType);
        await FetchStatsAsync();
    }

    private void UpdateMediaTypeUrl(string mediaType)
    {
        var uri = Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["type"] = string.IsNullOrEmpty(mediaType) ? null : mediaType,
            ["tab"] = null
        });

        Navigation.NavigateTo(uri, replace: true);
    }

    private async Task OnDateRangeChanged((DateOnly? From, DateOnly? To) range)
    {
        if (range.From is not null) _fromDate = range.From.Value;
        if (range.To is not null) _toDate = range.To.Value;
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
