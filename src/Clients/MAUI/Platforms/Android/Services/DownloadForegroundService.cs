using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using K7.Clients.MAUI.Services;
using Microsoft.Extensions.DependencyInjection;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using Log = Android.Util.Log;
using Resource = K7.Clients.MAUI.Resource;

namespace K7.Clients.MAUI.Platforms.Android.Services;

[Service(
    Name = "com.k7.maui.DownloadForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeDataSync)]
public class DownloadForegroundService : Service
{
    public const string ActionCancel = "com.k7.maui.DOWNLOAD_CANCEL";

    private const string Tag = "K7-Download";
    private const string ChannelId = "k7_downloads";
    private const int NotificationId = 2001;
    private static readonly TimeSpan NotifyThrottle = TimeSpan.FromMilliseconds(500);

    private IDownloadManager? _downloadManager;
    private DateTime _lastNotifyUtc;
    private bool _subscribed;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        EnsureChannel();
        StartAsForeground(BuildNotification());
        Subscribe();
        Log.Info(Tag, "DownloadForegroundService created");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionCancel)
        {
            _ = _downloadManager?.CancelAllAsync();
            return StartCommandResult.NotSticky;
        }

        StartAsForeground(BuildNotification());
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        Unsubscribe();
        base.OnDestroy();
        Log.Info(Tag, "DownloadForegroundService destroyed");
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        var services = IPlatformApplication.Current?.Services;
        _downloadManager = services?.GetService<IDownloadManager>();
        if (_downloadManager is null)
            return;

        _downloadManager.ProgressChanged += OnQueueChanged;
        _downloadManager.DownloadCompleted += OnDownloadCompleted;
        _downloadManager.DownloadFailed += OnDownloadFailed;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _downloadManager is null)
            return;

        _downloadManager.ProgressChanged -= OnQueueChanged;
        _downloadManager.DownloadCompleted -= OnDownloadCompleted;
        _downloadManager.DownloadFailed -= OnDownloadFailed;
        _subscribed = false;
    }

    private void OnQueueChanged(DownloadProgressInfo _) => RefreshNotification(throttled: true);

    private void OnDownloadCompleted(DownloadCompletedInfo _) => RefreshNotification(throttled: false);

    private void OnDownloadFailed(DownloadFailedInfo _) => RefreshNotification(throttled: false);

    private void RefreshNotification(bool throttled)
    {
        if (throttled && DateTime.UtcNow - _lastNotifyUtc < NotifyThrottle)
            return;

        _lastNotifyUtc = DateTime.UtcNow;
        var manager = NotificationManagerCompat.From(this);
        manager?.Notify(NotificationId, BuildNotification());
    }

    private void StartAsForeground(Notification notification)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        else
            StartForeground(NotificationId, notification);
    }

    private Notification BuildNotification()
    {
        var snapshot = _downloadManager is null
            ? default
            : DownloadQueueKeepAlive.CreateSnapshot(_downloadManager.Queue);

        var title = snapshot.ActiveCount > 1
            ? DownloadKeepAliveStrings.TitleWithCount(snapshot.ActiveCount)
            : DownloadKeepAliveStrings.Title;

        var current = snapshot.Current;
        var text = current is null
            ? DownloadKeepAliveStrings.Title
            : current.Status switch
            {
                DownloadItemStatus.Preparing => $"{current.Request.Title} - {DownloadKeepAliveStrings.Preparing}",
                DownloadItemStatus.Queued => $"{current.Request.Title} - {DownloadKeepAliveStrings.Queued}",
                _ => FormatProgress(current)
            };

        var indeterminate = current is null
            || current.Status is not DownloadItemStatus.Downloading
            || current.TotalBytes is null or <= 0;
        var percent = current is null
            ? 0
            : (int)Math.Clamp(current.Progress * 100, 0, 100);

        var openApp = PendingIntent.GetActivity(
            this,
            0,
            new Intent(this, typeof(MainActivity))
                .SetAction(Intent.ActionMain)
                .AddCategory(Intent.CategoryLauncher),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

        var cancelIntent = PendingIntent.GetService(
            this,
            1,
            new Intent(this, typeof(DownloadForegroundService)).SetAction(ActionCancel),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(title)!
            .SetContentText(text)!
            .SetSmallIcon(Resource.Drawable.ic_notification)!
            .SetColor(ContextCompat.GetColor(this, Resource.Color.colorAccent))
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetContentIntent(openApp)!
            .SetProgress(100, percent, indeterminate)!
            .AddAction(0, DownloadKeepAliveStrings.Cancel, cancelIntent)!;

        return builder.Build();
    }

    private static string FormatProgress(DownloadQueueItem item)
    {
        var percent = (int)Math.Clamp(item.Progress * 100, 0, 100);
        return item.TotalBytes is > 0
            ? $"{item.Request.Title} - {percent}%"
            : item.Request.Title;
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null)
            return;

        var channel = new NotificationChannel(ChannelId, DownloadKeepAliveStrings.ChannelName, NotificationImportance.Low);
        channel.SetShowBadge(false);
        channel.EnableVibration(false);
        channel.SetSound(null, null);
        manager.CreateNotificationChannel(channel);
    }
}
