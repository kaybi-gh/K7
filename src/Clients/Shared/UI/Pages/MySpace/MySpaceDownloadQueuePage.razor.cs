using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceDownloadQueuePage : ComponentBase, IDisposable
{
    private List<DownloadQueueItem> _items = [];
    private bool _progressDirty;
    private System.Timers.Timer? _progressThrottle;

    protected override void OnInitialized()
    {
        RefreshItems();

        _progressThrottle = new System.Timers.Timer(500) { AutoReset = false };
        _progressThrottle.Elapsed += async (_, _) =>
        {
            if (!_progressDirty) return;
            _progressDirty = false;
            await InvokeAsync(StateHasChanged);
        };

        DownloadManager.ProgressChanged += OnProgressChanged;
        DownloadManager.DownloadCompleted += OnDownloadCompleted;
    }

    private void RefreshItems()
    {
        _items = DownloadManager.Queue
            .Where(q => q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading)
            .ToList();
    }

    private void OnProgressChanged(DownloadProgressInfo info)
    {
        RefreshItems();
        _progressDirty = true;
        _progressThrottle?.Start();
    }

    private void OnDownloadCompleted(DownloadCompletedInfo info) => OnDownloadCompletedAsync(info).FireAndForget();

    private async Task OnDownloadCompletedAsync(DownloadCompletedInfo info)
    {
        await InvokeAsync(() =>
        {
            RefreshItems();
            StateHasChanged();
        });
    }

    private async Task CancelAsync(Guid downloadId)
    {
        await DownloadManager.CancelAsync(downloadId);
        RefreshItems();
        StateHasChanged();
    }

    private async Task CancelAllAsync()
    {
        await DownloadManager.CancelAllAsync();
        RefreshItems();
        StateHasChanged();
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/my-space/downloads");
    }

    private string GetStatusLabel(DownloadItemStatus status) => status switch
    {
        DownloadItemStatus.Queued => L["StatusQueued"],
        DownloadItemStatus.Preparing => L["StatusPreparing"],
        DownloadItemStatus.Downloading => L["StatusDownloading"],
        _ => status.ToString()
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public void Dispose()
    {
        _progressThrottle?.Dispose();
        DownloadManager.ProgressChanged -= OnProgressChanged;
        DownloadManager.DownloadCompleted -= OnDownloadCompleted;
    }
}
