using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.UI.Helpers;

public sealed class MediaMetadataRefreshWatcher : IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly Func<Func<Task>, Task> _invokeAsync;
    private Guid? _mediaId;
    private Func<Task>? _reloadAfterMetadataRefresh;
    private Func<Task>? _reloadAfterPicturesUpdated;
    private CancellationTokenSource? _picturesDebounceCts;

    public MediaMetadataRefreshWatcher(K7HubClient hubClient, Func<Func<Task>, Task> invokeAsync)
    {
        _hubClient = hubClient;
        _invokeAsync = invokeAsync;
        _hubClient.MediaMetadataRefreshed += OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated += OnMediaPicturesUpdated;
    }

    public void Watch(Guid mediaId, Func<Task> reloadAfterMetadataRefresh, Func<Task>? reloadAfterPicturesUpdated = null)
    {
        _mediaId = mediaId;
        _reloadAfterMetadataRefresh = reloadAfterMetadataRefresh;
        _reloadAfterPicturesUpdated = reloadAfterPicturesUpdated ?? reloadAfterMetadataRefresh;
    }

    private void OnMediaMetadataRefreshed(Guid mediaId)
    {
        if (_mediaId != mediaId || _reloadAfterMetadataRefresh is null)
            return;

        _ = _invokeAsync(_reloadAfterMetadataRefresh);
    }

    private void OnMediaPicturesUpdated(Guid mediaId)
    {
        if (_mediaId != mediaId || _reloadAfterPicturesUpdated is null)
            return;

        _picturesDebounceCts?.Cancel();
        _picturesDebounceCts?.Dispose();
        _picturesDebounceCts = new CancellationTokenSource();
        var token = _picturesDebounceCts.Token;

        _ = DebouncedPicturesReloadAsync(token);
    }

    private async Task DebouncedPicturesReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            await _invokeAsync(_reloadAfterPicturesUpdated!);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _hubClient.MediaMetadataRefreshed -= OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated -= OnMediaPicturesUpdated;
        _picturesDebounceCts?.Cancel();
        _picturesDebounceCts?.Dispose();
    }
}
