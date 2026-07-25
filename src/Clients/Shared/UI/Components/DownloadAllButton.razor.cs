using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class DownloadAllButton : ComponentBase, IDisposable
{
    [Parameter, EditorRequired]
    public IReadOnlyList<DownloadRequest> Items { get; set; } = [];

    /// <summary>Display name used in confirm dialogs (album, artist, playlist, ...).</summary>
    [Parameter]
    public string? Label { get; set; }

    private bool _isEnqueuing;
    private bool _allDownloaded;
    private bool _isDownloading;
    private Guid[] _itemIds = [];

    protected override void OnInitialized()
    {
        DownloadManager.DownloadCompleted += OnDownloadCompleted;
        DownloadManager.DownloadFailed += OnDownloadFailed;
    }

    protected override async Task OnParametersSetAsync()
    {
        var ids = Items.Select(i => i.IndexedFileId).ToArray();
        if (ids.SequenceEqual(_itemIds))
            return;

        _itemIds = ids;
        await RefreshStateAsync();
    }

    private async Task RefreshStateAsync()
    {
        if (Items.Count == 0)
        {
            _allDownloaded = false;
            _isDownloading = false;
            return;
        }

        var downloaded = 0;
        foreach (var item in Items)
        {
            if (await OfflineStore.IsAvailableOfflineAsync(item.IndexedFileId))
                downloaded++;
        }

        _allDownloaded = downloaded == Items.Count;
        _isDownloading = !_allDownloaded && Items.Any(item =>
            DownloadManager.Queue.Any(q =>
                q.Request.IndexedFileId == item.IndexedFileId &&
                q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading));
    }

    private Task OnClickAsync() =>
        _allDownloaded ? RemoveDownloadsAsync() : DownloadAllAsync();

    private async Task CancelDownloadsAsync()
    {
        var active = DownloadManager.Queue
            .Where(q =>
                Items.Any(i => i.IndexedFileId == q.Request.IndexedFileId) &&
                q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading)
            .ToList();

        if (active.Count == 0)
        {
            _isDownloading = false;
            await RefreshStateAsync();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!await ConfirmAsync(
                L["ConfirmCancelTitle"],
                string.Format(L["ConfirmCancelMessage"], ResolveLabel(), active.Count),
                S["Confirm"]))
            return;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        foreach (var item in active)
            await DownloadManager.CancelAsync(item.DownloadId);

        _isDownloading = false;
        Snackbar.Add(L["DownloadsCancelled"], K7Severity.Info);
        await RefreshStateAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAllAsync()
    {
        if (_isEnqueuing || _allDownloaded || Items.Count == 0) return;

        var pending = new List<DownloadRequest>();
        foreach (var item in Items)
        {
            if (!await OfflineStore.IsAvailableOfflineAsync(item.IndexedFileId))
                pending.Add(item);
        }

        if (pending.Count == 0)
        {
            _allDownloaded = true;
            return;
        }

        if (!await ConfirmAsync(
                L["ConfirmDownloadTitle"],
                string.Format(L["ConfirmDownloadMessage"], ResolveLabel(), pending.Count),
                S["Confirm"]))
            return;

        // Let the confirm dialog finish closing before heavy work / re-renders.
        _isEnqueuing = true;
        _isDownloading = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            foreach (var item in pending)
                await DownloadManager.EnqueueAsync(item);

            Snackbar.Add(string.Format(L["DownloadAllQueued"], pending.Count), K7Severity.Info);
        }
        finally
        {
            _isEnqueuing = false;
            await RefreshStateAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RemoveDownloadsAsync()
    {
        if (Items.Count == 0) return;

        if (!await ConfirmAsync(
                L["ConfirmRemoveTitle"],
                string.Format(L["ConfirmRemoveMessage"], ResolveLabel(), Items.Count),
                S["Delete"]))
            return;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        foreach (var item in Items)
            await OfflineStore.RemoveAsync(item.IndexedFileId);

        _allDownloaded = false;
        Snackbar.Add(L["DownloadsRemoved"], K7Severity.Info);
        await InvokeAsync(StateHasChanged);
    }

    private async Task<bool> ConfirmAsync(string title, string message, string yesText)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            title,
            message,
            yesText: yesText,
            cancelText: S["Cancel"]);
        return confirmed == true;
    }

    private string ResolveLabel() =>
        string.IsNullOrWhiteSpace(Label) ? L["DefaultLabel"] : Label;

    private void OnDownloadCompleted(DownloadCompletedInfo info)
    {
        if (Items.Any(i => i.IndexedFileId == info.Request.IndexedFileId))
            _ = InvokeAsync(RefreshAndRenderAsync);
    }

    private void OnDownloadFailed(DownloadFailedInfo info)
    {
        if (Items.Any(i => i.IndexedFileId == info.Request.IndexedFileId))
            _ = InvokeAsync(RefreshAndRenderAsync);
    }

    private async Task RefreshAndRenderAsync()
    {
        if (_isEnqueuing) return;
        await RefreshStateAsync();
        StateHasChanged();
    }

    public void Dispose()
    {
        DownloadManager.DownloadCompleted -= OnDownloadCompleted;
        DownloadManager.DownloadFailed -= OnDownloadFailed;
    }
}
