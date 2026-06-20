using ApexCharts;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class ServerHealthCharts
{
    [Parameter] public IReadOnlyList<ServerMetricsSnapshotDto> Snapshots { get; set; } = [];

    private List<ChartDataPoint> _cpuData = [];
    private List<ChartDataPoint> _memoryData = [];
    private List<ChartDataPoint> _networkData = [];
    private int _builtSnapshotCount = -1;
    private DateTime _builtSnapshotTimestamp;

    private ServerMetricsSnapshotDto? Latest => Snapshots.Count > 0 ? Snapshots[^1] : null;

    private readonly ApexChartOptions<ChartDataPoint> _cpuChartOptions = CreateAreaChartOptions(max: 100);
    private readonly ApexChartOptions<ChartDataPoint> _memoryChartOptions = CreateAreaChartOptions(max: 100);
    private readonly ApexChartOptions<ChartDataPoint> _networkChartOptions = CreateAreaChartOptions();

    protected override void OnParametersSet()
    {
        if (Snapshots.Count == 0)
        {
            if (_cpuData.Count > 0)
            {
                _cpuData = [];
                _memoryData = [];
                _networkData = [];
            }

            _builtSnapshotCount = 0;
            return;
        }

        var latest = Snapshots[^1];
        if (Snapshots.Count == _builtSnapshotCount && latest.Timestamp == _builtSnapshotTimestamp)
            return;

        _builtSnapshotCount = Snapshots.Count;
        _builtSnapshotTimestamp = latest.Timestamp;
        RebuildCharts();
    }

    private void RebuildCharts()
    {
        _cpuData = [];
        _memoryData = [];
        _networkData = [];

        foreach (var snapshot in Snapshots)
        {
            var label = snapshot.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            var memoryPercent = snapshot.MemoryTotalMb > 0
                ? (decimal)(snapshot.MemoryUsedMb / snapshot.MemoryTotalMb * 100)
                : 0m;

            _cpuData.Add(new ChartDataPoint(label, (decimal)snapshot.CpuPercent));
            _memoryData.Add(new ChartDataPoint(label, memoryPercent));
            _networkData.Add(new ChartDataPoint(label, (decimal)snapshot.NetworkMbps));
        }
    }

    private static string FormatMemoryPercent(ServerMetricsSnapshotDto snapshot)
    {
        if (snapshot.MemoryTotalMb <= 0)
            return "0%";

        var percent = snapshot.MemoryUsedMb / snapshot.MemoryTotalMb * 100;
        return $"{percent:0.#}%";
    }

    private static ApexChartOptions<ChartDataPoint> CreateAreaChartOptions(decimal? max = null)
    {
        var options = new ApexChartOptions<ChartDataPoint>
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
            Xaxis = new XAxis { Labels = new XAxisLabels { Show = false } },
            Yaxis = [new YAxis { Min = 0, Max = max }],
            Grid = new Grid { Show = false },
            Theme = new Theme { Mode = Mode.Dark }
        };

        return options;
    }
}
