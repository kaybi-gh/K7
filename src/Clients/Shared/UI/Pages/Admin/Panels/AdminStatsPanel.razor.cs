using ApexCharts;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Users;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminStatsPanel
{
    private WatchStatsDto? _stats;
    private bool _loading = true;
    private string _selectedPeriod = "month";
    private string _selectedMediaType = "";
    private Guid? _selectedUserId;
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);
    private List<UserDto> _users = [];

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

    private readonly List<ButtonGroupOption<string>> _periodOptions =
    [
        new("week", Label: "Sem."),
        new("month", Label: "Mois"),
        new("year", Label: "An"),
        new("all", Label: "Tout"),
        new("custom", Label: "Perso.")
    ];

    private readonly List<ButtonGroupOption<string>> _mediaTypeOptions =
    [
        new("", Label: "Tous"),
        new("MusicTrack", Label: "Musique"),
        new("Movie", Label: "Films"),
        new("SerieEpisode", Label: "Series")
    ];

    private string TopItemsLabel => _selectedMediaType switch
    {
        "MusicTrack" => L["TopTracks"],
        "Movie" => L["TopMovies"],
        "SerieEpisode" => L["TopEpisodes"],
        _ => L["TopItems"]
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _users = await UserAdminService.GetUsersAsync();
        }
        catch
        {
            _users = [];
        }

        await FetchStatsAsync();
    }

    private async Task OnUserChanged(Guid? userId)
    {
        _selectedUserId = userId;
        await FetchStatsAsync();
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
        await FetchStatsAsync();
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

            _stats = await K7ServerService.GetAdminWatchStatsAsync(mediaTypeParam, _selectedPeriod, _selectedUserId, from, to);
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

    private static bool HasNonZeroData(List<ChartDataPoint> data) =>
        data.Count > 0 && data.Any(d => d.Value > 0);

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
}
