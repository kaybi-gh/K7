using Android.Content;
using K7.Clients.MAUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public sealed class AndroidDownloadKeepAlive : IDownloadKeepAlive
{
    private readonly ILogger<AndroidDownloadKeepAlive> _logger;
    private readonly object _gate = new();
    private bool _active;
    private CancellationTokenSource? _stopCts;

    public AndroidDownloadKeepAlive(ILogger<AndroidDownloadKeepAlive> logger)
    {
        _logger = logger;
    }

    public void SetActive(bool active)
    {
        lock (_gate)
        {
            if (active)
            {
                _stopCts?.Cancel();
                _stopCts = null;
                if (_active)
                    return;

                _active = true;
                StartService();
                return;
            }

            if (!_active)
                return;

            _active = false;
            _stopCts = new CancellationTokenSource();
            var token = _stopCts.Token;
            _ = StopAfterDelayAsync(token);
        }
    }

    private void StartService()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(DownloadForegroundService));
            context.StartForegroundService(intent);
            _logger.LogInformation("Started Android download foreground service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Android download foreground service");
        }
    }

    private async Task StopAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Give OnStartCommand time to call StartForeground after a short-lived enqueue.
            await Task.Delay(1500, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_active)
                return;

            try
            {
                var context = global::Android.App.Application.Context;
                context.StopService(new Intent(context, typeof(DownloadForegroundService)));
                _logger.LogInformation("Stopped Android download foreground service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Android download foreground service");
            }
        }
    }
}
