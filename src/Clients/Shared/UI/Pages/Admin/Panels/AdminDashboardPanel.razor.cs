using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
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
    private int _activeStreamCount;
    private int _errorCount;
    private int _runningTaskCount;
    private PeriodicTimer? _metricsPollTimer;
    private CancellationTokenSource? _pollCts;

    private int OnlineUsersCount =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].OnlineUsersCount : 0;

    private IReadOnlyList<ServerDiskVolumeDto> DiskVolumes =>
        _metricSnapshots.Count > 0 ? _metricSnapshots[^1].DiskVolumes : [];

    protected override async Task OnInitializedAsync()
    {
        HubClient.ActiveStreamsUpdated += OnActiveStreamsUpdated;
        HubClient.ServerMetricsUpdated += OnServerMetricsUpdated;

        await Task.WhenAll(LoadKpisAsync(), LoadMetricsHistoryAsync(initialLoad: true));

        try
        {
            await HubClient.JoinAdminStreamsGroupAsync();
        }
        catch
        {
        }

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
            var streams = await K7ServerService.GetActiveStreamsAsync();
            _activeStreamCount = streams?.Count ?? 0;
        }
        catch
        {
            _activeStreamCount = 0;
        }

        try
        {
            var summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
            _errorCount = summaries.Sum(s =>
                s.OrphanIndexedFileCount + s.UnidentifiedIndexedFileCount + s.MissingFileMetadataCount
                + s.MissingHlsSegmentsCount + s.MediaMissingPicturesCount + s.MediaMissingMetadataCount
                + s.MediaWithoutFilesCount + s.InaccessiblePathCount);
        }
        catch
        {
            _errorCount = 0;
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

    private void OnActiveStreamsUpdated(IReadOnlyList<ActiveStreamDto> streams)
    {
        InvokeAsync(() =>
        {
            if (_activeStreamCount == streams.Count)
                return;

            _activeStreamCount = streams.Count;
            StateHasChanged();
        });
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
        HubClient.ActiveStreamsUpdated -= OnActiveStreamsUpdated;
        HubClient.ServerMetricsUpdated -= OnServerMetricsUpdated;

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _metricsPollTimer?.Dispose();
    }
}
