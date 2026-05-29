#if ANDROID
using Android.Content;
using K7.Clients.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using AndroidX.MediaRouter.Media;
using Com.Google.Android.Gms.Cast;
using Com.Google.Android.Gms.Cast.Framework;
using Com.Google.Android.Gms.Cast.Framework.Media;
using Com.Google.Android.Gms.Common.Api;

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// Google Cast SDK integration for Android.
/// Discovers Chromecast devices and manages casting sessions.
/// </summary>
internal sealed class AndroidCastService : ICastService, IDisposable
{
    private readonly ILogger<AndroidCastService> _logger;
    private CastContext? _castContext;
    private SessionManager? _sessionManager;
    private CastSessionManagerListener? _sessionListener;
    private MediaRouter? _mediaRouter;
    private MediaRouteSelector? _mediaRouteSelector;
    private CastMediaRouterCallback? _routerCallback;
    private readonly List<CastDeviceInfo> _devices = [];
    private bool _isCasting;

    public bool IsAvailable => _castContext is not null;
    public bool IsCasting => _isCasting;
    public IReadOnlyList<CastDeviceInfo> DiscoveredDevices => _devices;

    public event Action? StateChanged;
    public event Action<IReadOnlyList<CastDeviceInfo>>? DevicesDiscovered;
#pragma warning disable CS0067
    public event Action<CastMediaStatus>? MediaStatusUpdated;
#pragma warning restore CS0067

    public AndroidCastService(ILogger<AndroidCastService> logger)
    {
        _logger = logger;
        InitializeCastContext();
    }

    private void InitializeCastContext()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Platform.AppContext;
            var castOptions = new CastOptions.Builder(CastMediaControlIntent.DefaultMediaReceiverApplicationId)
                .Build();

            CastContext.SetSharedInstance(context, castOptions);
            _castContext = CastContext.GetSharedInstance(context);
            _sessionManager = _castContext.SessionManager;

            _sessionListener = new CastSessionManagerListener(this);
            _sessionManager.AddSessionManagerListener(_sessionListener);

            _mediaRouter = MediaRouter.GetInstance(context);
            _mediaRouteSelector = new MediaRouteSelector.Builder()
                .AddControlCategory(CastMediaControlIntent.CategoryForCast(CastMediaControlIntent.DefaultMediaReceiverApplicationId))
                .Build();

            _logger.LogInformation("Google Cast SDK initialized");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Google Cast SDK");
        }
    }

    public Task StartDiscoveryAsync()
    {
        if (_mediaRouter is null || _mediaRouteSelector is null) return Task.CompletedTask;

        _routerCallback = new CastMediaRouterCallback(this);
        _mediaRouter.AddCallback(_mediaRouteSelector, _routerCallback, MediaRouter.CallbackFlagPerformActiveScan);
        _logger.LogDebug("Started Cast device discovery");
        return Task.CompletedTask;
    }

    public Task StopDiscoveryAsync()
    {
        if (_mediaRouter is null || _routerCallback is null) return Task.CompletedTask;

        _mediaRouter.RemoveCallback(_routerCallback);
        _routerCallback = null;
        _logger.LogDebug("Stopped Cast device discovery");
        return Task.CompletedTask;
    }

    public async Task CastAsync(CastMediaRequest request)
    {
        var castSession = _sessionManager?.CurrentCastSession;
        if (castSession is null)
        {
            _logger.LogWarning("No active Cast session");
            return;
        }

        var remoteMediaClient = castSession.RemoteMediaClient;
        if (remoteMediaClient is null) return;

        var metadata = new MediaMetadata(MediaMetadata.MediaTypeMovie);
        if (!string.IsNullOrEmpty(request.Title))
        {
            metadata.PutString(MediaMetadata.KeyTitle, request.Title);
        }
        if (!string.IsNullOrEmpty(request.Subtitle))
        {
            metadata.PutString(MediaMetadata.KeySubtitle, request.Subtitle);
        }
        if (!string.IsNullOrEmpty(request.ThumbnailUrl))
        {
            metadata.AddImage(new Com.Google.Android.Gms.Common.Images.WebImage(global::Android.Net.Uri.Parse(request.ThumbnailUrl)!));
        }

        var mediaInfoBuilder = new MediaInfo.Builder(request.StreamUrl)
            .SetStreamType(MediaInfo.StreamTypeBuffered)
            .SetContentType(request.ContentType)
            .SetMetadata(metadata);

        if (request.Duration.HasValue)
        {
            mediaInfoBuilder.SetStreamDuration((long)(request.Duration.Value * 1000));
        }

        var mediaInfo = mediaInfoBuilder.Build();

        var loadOptions = new MediaLoadOptions.Builder()
            .SetAutoplay(true)
            .SetPlayPosition((long)(request.StartPosition * 1000))
            .Build();

        await Task.Run(() => remoteMediaClient.Load(mediaInfo, loadOptions));
        _isCasting = true;
        StateChanged?.Invoke();
    }

    public Task StopCastingAsync()
    {
        var castSession = _sessionManager?.CurrentCastSession;
        castSession?.RemoteMediaClient?.Stop();
        _sessionManager?.EndCurrentSession(true);
        _isCasting = false;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task SendTransportCommandAsync(CastTransportCommand command)
    {
        var remoteMediaClient = _sessionManager?.CurrentCastSession?.RemoteMediaClient;
        if (remoteMediaClient is null) return Task.CompletedTask;

        switch (command)
        {
            case CastTransportCommand.Play:
                remoteMediaClient.Play();
                break;
            case CastTransportCommand.Pause:
                remoteMediaClient.Pause();
                break;
            case CastTransportCommand.Stop:
                remoteMediaClient.Stop();
                _isCasting = false;
                StateChanged?.Invoke();
                break;
        }

        return Task.CompletedTask;
    }

    internal void OnDevicesUpdated()
    {
        if (_mediaRouter is null) return;

        _devices.Clear();
        foreach (var route in _mediaRouter.Routes)
        {
            if (route.IsDefault || !route.IsEnabled) continue;
            if (route.ConnectionState == MediaRouter.RouteInfo.ConnectionStateDisconnected
                || string.IsNullOrEmpty(route.Name)) continue;

            _devices.Add(new CastDeviceInfo(route.Id, route.Name));
        }

        DevicesDiscovered?.Invoke(_devices);
        StateChanged?.Invoke();
    }

    internal void OnSessionStarted()
    {
        _isCasting = true;
        StateChanged?.Invoke();
    }

    internal void OnSessionEnded()
    {
        _isCasting = false;
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_sessionManager is not null && _sessionListener is not null)
        {
            _sessionManager.RemoveSessionManagerListener(_sessionListener);
        }

        if (_mediaRouter is not null && _routerCallback is not null)
        {
            _mediaRouter.RemoveCallback(_routerCallback);
        }
    }

    private sealed class CastSessionManagerListener(AndroidCastService service) : Java.Lang.Object, ISessionManagerListener
    {
        public void OnSessionEnded(Session p0, int p1) => service.OnSessionEnded();
        public void OnSessionEnding(Session p0) { }
        public void OnSessionResumeFailed(Session p0, int p1) { }
        public void OnSessionResumed(Session p0, bool p1) => service.OnSessionStarted();
        public void OnSessionResuming(Session p0, string p1) { }
        public void OnSessionStartFailed(Session p0, int p1) { }
        public void OnSessionStarted(Session p0, string p1) => service.OnSessionStarted();
        public void OnSessionStarting(Session p0) { }
        public void OnSessionSuspended(Session p0, int p1) { }
    }

    private sealed class CastMediaRouterCallback(AndroidCastService service) : MediaRouter.Callback
    {
        public override void OnRouteAdded(MediaRouter router, MediaRouter.RouteInfo route) => service.OnDevicesUpdated();
        public override void OnRouteRemoved(MediaRouter router, MediaRouter.RouteInfo route) => service.OnDevicesUpdated();
        public override void OnRouteChanged(MediaRouter router, MediaRouter.RouteInfo route) => service.OnDevicesUpdated();
    }
}
#endif
