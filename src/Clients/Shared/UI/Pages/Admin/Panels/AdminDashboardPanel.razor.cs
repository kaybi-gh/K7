using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDashboardPanel : IDisposable
{
    private const int MaxMetricPoints = 72;
    private static readonly TimeSpan MetricsPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DesktopTaskKpiDebounce = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan TvTaskKpiDebounce = TimeSpan.FromSeconds(10);

    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    private IReadOnlyList<ServerMetricsSnapshotDto> _metricSnapshots = [];
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private int _runningTaskCount;
    private bool _isTv;
    private PeriodicTimer? _metricsPollTimer;
    private CancellationTokenSource? _pollCts;
    private Timer? _taskKpiDebounceTimer;
    private readonly CancellationTokenSource _cts = new();

    private TimeSpan TaskKpiDebounce => _isTv ? TvTaskKpiDebounce : DesktopTaskKpiDebounce;

    private int OnlineUsersCount =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].OnlineUsersCount : 0;

    private IReadOnlyList<ServerDiskVolumeDto> DiskVolumes =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].DiskVolumes : [];

    protected override void OnInitialized()
    {
        _isTv = DeviceService.CachedDeviceType == DeviceType.TV;
    }

    protected override async Task OnInitializedAsync()
    {
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        HubClient.ServerMetricsUpdated += OnServerMetricsUpdated;
        HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;

        await Task.WhenAll(LoadKpisAsync(), LoadMetricsHistoryAsync(initialLoad: true));

        if (_isTv)
            return;

        _pollCts = new CancellationTokenSource();
        _metricsPollTimer = new PeriodicTimer(MetricsPollInterval);
        _ = PollMetricsAsync(_pollCts.Token);
    }

    private async Task PollMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_metricsPollTimer is not null && await _metricsPollTimer.WaitForNextTickAsync(cancellationToken))
                await LoadMetricsHistoryAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadKpisAsync()
    {
        await Task.WhenAll(LoadDiagnosticsKpiAsync(), LoadTaskKpiAsync());
    }

    private async Task LoadDiagnosticsKpiAsync()
    {
        try
        {
            var summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync(_cts.Token);
            _errorCount = LibraryHealthSummaryCounts.SumErrors(summaries);
            _warningCount = LibraryHealthSummaryCounts.SumWarnings(summaries);
            _infoCount = LibraryHealthSummaryCounts.SumInfo(summaries);
        }
        catch
        {
            _errorCount = 0;
            _warningCount = 0;
            _infoCount = 0;
        }
    }

    private async Task LoadTaskKpiAsync()
    {
        try
        {
            var summary = await BackgroundTaskService.GetSummaryAsync(cancellationToken: _cts.Token);
            _runningTaskCount = summary.StatusCounts
                .Where(s => s.Status is BackgroundTaskStatus.InProgress or BackgroundTaskStatus.Pending or BackgroundTaskStatus.WaitingForRetry)
                .Sum(s => s.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _runningTaskCount = 0;
        }
    }

    private async Task LoadMetricsHistoryAsync(bool initialLoad = false)
    {
        try
        {
            var history = await K7ServerService.GetServerMetricsAsync();
            if (history?.Snapshots is not { Count: > 0 } snapshots)
                return;

            if (initialLoad || _metricSnapshots.Count == 0)
            {
                if (TryApplySnapshots(snapshots))
                    await InvokeAsync(StateHasChanged);

                return;
            }

            var lastRemote = snapshots[^1];
            if (_metricSnapshots.Count > 0 && lastRemote.Timestamp == _metricSnapshots[^1].Timestamp)
                return;

            if (TryAppendSnapshot(lastRemote))
                await InvokeAsync(StateHasChanged);
        }
        catch
        {
        }
    }

    private void OnServerMetricsUpdated(ServerMetricsSnapshotDto snapshot)
    {
        InvokeAsync(() =>
        {
            if (!TryAppendSnapshot(snapshot))
                return;

            StateHasChanged();
        });
    }

    private void OnBackgroundTaskUpdated()
    {
        _taskKpiDebounceTimer?.Dispose();
        _taskKpiDebounceTimer = new Timer(_ =>
        {
            _ = InvokeAsync(async () =>
            {
                await LoadTaskKpiAsync();
                StateHasChanged();
            });
        }, null, TaskKpiDebounce, Timeout.InfiniteTimeSpan);
    }

    private bool TryApplySnapshots(IReadOnlyList<ServerMetricsSnapshotDto> snapshots)
    {
        if (_isTv)
        {
            var latest = snapshots[^1];
            if (_metricSnapshots.Count == 1 && _metricSnapshots[0].Timestamp == latest.Timestamp)
                return false;

            _metricSnapshots = [latest];
            return true;
        }

        if (_metricSnapshots.Count == snapshots.Count
            && snapshots.Count > 0
            && _metricSnapshots[^1].Timestamp == snapshots[^1].Timestamp)
        {
            return false;
        }

        _metricSnapshots = snapshots.ToList();
        return true;
    }

    private bool TryAppendSnapshot(ServerMetricsSnapshotDto snapshot)
    {
        if (_metricSnapshots.Count > 0 && _metricSnapshots[^1].Timestamp == snapshot.Timestamp)
            return false;

        if (_isTv)
        {
            _metricSnapshots = [snapshot];
            return true;
        }

        var next = _metricSnapshots.ToList();
        next.Add(snapshot);

        while (next.Count > MaxMetricPoints)
            next.RemoveAt(0);

        _metricSnapshots = next;
        return true;
    }

    public void Dispose()
    {
        HubClient.ServerMetricsUpdated -= OnServerMetricsUpdated;
        HubClient.BackgroundTaskUpdated -= OnBackgroundTaskUpdated;

        _cts.Cancel();
        _cts.Dispose();
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _metricsPollTimer?.Dispose();
        _taskKpiDebounceTimer?.Dispose();
    }
}
