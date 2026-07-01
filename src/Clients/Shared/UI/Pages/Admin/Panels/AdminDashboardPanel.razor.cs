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

    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private K7HubClient HubClient { get; set; } = default!;

    private IReadOnlyList<ServerMetricsSnapshotDto> _metricSnapshots = [];
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private int _runningTaskCount;
    private PeriodicTimer? _metricsPollTimer;
    private CancellationTokenSource? _pollCts;

    private int OnlineUsersCount =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].OnlineUsersCount : 0;

    private IReadOnlyList<ServerDiskVolumeDto> DiskVolumes =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].DiskVolumes : [];

    protected override async Task OnInitializedAsync()
    {
        HubClient.ServerMetricsUpdated += OnServerMetricsUpdated;

        await Task.WhenAll(LoadKpisAsync(), LoadMetricsHistoryAsync(initialLoad: true));

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
        try
        {
            var summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
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

        try
        {
            var summary = await BackgroundTaskService.GetSummaryAsync();
            _runningTaskCount = summary.StatusCounts
                .Where(s => s.Status is BackgroundTaskStatus.InProgress or BackgroundTaskStatus.Pending or BackgroundTaskStatus.WaitingForRetry)
                .Sum(s => s.Count);
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

    private bool TryApplySnapshots(IReadOnlyList<ServerMetricsSnapshotDto> snapshots)
    {
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

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _metricsPollTimer?.Dispose();
    }
}
