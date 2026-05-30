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
    private List<UserDto> _users = [];

    private List<ChartDataPoint> _playsOverTimeData = [];
    private List<ChartDataPoint> _hourData = [];
    private List<ChartDataPoint> _dowData = [];
    private List<ChartDataPoint> _genreData = [];
    private List<ChartDataPoint> _deviceData = [];

    private ApexChartOptions<ChartDataPoint> _areaChartOptions = CreateAreaChartOptions();
    private ApexChartOptions<ChartDataPoint> _barChartOptions = CreateBarChartOptions();
    private ApexChartOptions<ChartDataPoint> _donutChartOptions = CreateDonutChartOptions();

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
        await FetchStatsAsync();
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        await FetchStatsAsync();
    }

    private async Task FetchStatsAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;
            _stats = await K7ServerService.GetAdminWatchStatsAsync(mediaTypeParam, _selectedPeriod, _selectedUserId);
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
            .Select(p => new ChartDataPoint(p.Hour.ToString(), p.Count))
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
}
