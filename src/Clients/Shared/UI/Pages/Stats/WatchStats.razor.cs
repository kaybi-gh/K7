using K7.Shared.Dtos;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class WatchStats : IDisposable
{
    private WatchStatsDto? _stats;
    private bool _loading = true;
    private string _selectedPeriod = "month";
    private string _selectedMediaType = "";
    private Timer? _debounceTimer;

    private List<ChartSeries<double>> _playsOverTimeSeries = [];
    private string[] _playsOverTimeLabels = [];
    private List<ChartSeries<double>> _hourSeries = [];
    private string[] _hourLabels = [];
    private List<ChartSeries<double>> _dowSeries = [];
    private string[] _dowLabels = [];

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.ProgressUpdated += OnProgressUpdated;
        await FetchStatsAsync();
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
            _stats = await K7ServerService.GetWatchStatsAsync(mediaTypeParam, _selectedPeriod);
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

        if (_stats.PlaysOverTime.Count > 0)
        {
            _playsOverTimeLabels = _stats.PlaysOverTime.Select(p => p.Date.ToString("MM/dd")).ToArray();
            _playsOverTimeSeries =
            [
                new() { Name = "Plays", Data = new(_stats.PlaysOverTime.Select(p => (double)p.Count).ToArray()) }
            ];
        }

        if (_stats.PlaysByHourOfDay.Count > 0)
        {
            _hourLabels = _stats.PlaysByHourOfDay.Select(p => p.Hour.ToString()).ToArray();
            _hourSeries =
            [
                new() { Name = "Plays", Data = new(_stats.PlaysByHourOfDay.Select(p => (double)p.Count).ToArray()) }
            ];
        }

        if (_stats.PlaysByDayOfWeek.Count > 0)
        {
            _dowLabels = _stats.PlaysByDayOfWeek.Select(p => p.Name[..3]).ToArray();
            _dowSeries =
            [
                new() { Name = "Plays", Data = new(_stats.PlaysByDayOfWeek.Select(p => (double)p.Count).ToArray()) }
            ];
        }
    }

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        _debounceTimer?.Dispose();
    }
}
