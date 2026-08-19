using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IAppExitService AppExitService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IConnectivityService Connectivity { get; set; } = default!;
    [Inject] private IFeedHubHostService FeedHub { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;
    [Inject] private SoftKeyboardJsBridge SoftKeyboardBridge { get; set; } = default!;
    [Inject] private IWindowsStreamFetchJsBridge WindowsStreamFetchBridge { get; set; } = default!;
    [Inject] private WebViewJsBridge WebViewJsBridge { get; set; } = default!;
    [Inject] private IPlaybackSyncService PlaybackSync { get; set; } = default!;

    private K7ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private bool _reconnectAnimationPlayed;
    private bool _firstRenderDone;
    private Timer? _overlayTimer;
    private DotNetObjectReference<MainLayout>? _selfRef;
    private ElementReference _reconnectAnimationContainer;

    private string? _sessionUserId;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;

        if (DeviceService.GetClientType() == ClientType.Native && System.OperatingSystem.IsWindows())
            WebViewJsBridge.SetRuntime(JS);

        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        ThemeBootstrap.InitializeAsync(ThemeService, JS, ServerInfoService)
            .FireAndForget(Logger, "Theme bootstrap failed");

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            K7HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        }

        await EnsureUserSessionAsync();
        await BindFeedHubAsync();
        FeedHub.Changed += OnFeedHubChanged;
    }

    private void OnFeedHubChanged() => InvokeAsync(StateHasChanged).FireAndForget(Logger);

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) =>
        OnAuthenticationStateChangedAsync(task).FireAndForget(Logger);

    private async Task OnAuthenticationStateChangedAsync(Task<AuthenticationState> task)
    {
        try
        {
            await task;
            await EnsureUserSessionAsync();
            await BindFeedHubAsync();
            if (_firstRenderDone)
                RequestOfflineStatsSync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hub session refresh failed");
        }
    }

    private async Task EnsureUserSessionAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var isAuth = authState.User.Identity?.IsAuthenticated == true;
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? authState.User.FindFirst("sub")?.Value;

            if (!isAuth || string.IsNullOrEmpty(userId))
            {
                _sessionUserId = null;
                return;
            }

            var userChanged = !string.Equals(_sessionUserId, userId, StringComparison.Ordinal);
            _sessionUserId = userId;

            if (userChanged && Connectivity.IsOnline)
                DeviceInitializer.InitializeDeviceAsync(Services, userId)
                    .FireAndForget(Logger, "Device init failed");

            var canReport = await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress);
            AudioProgressTracker.SetCanReport(canReport);

            var deviceStorageService = Services.GetRequiredService<IDeviceStorageService>();
            var deviceId = deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

            var baseUri = DeviceService.GetClientType() == ClientType.Web
                ? NavigationManager.ToAbsoluteUri("/")
                : K7ServerService.HttpClient.BaseAddress ?? NavigationManager.ToAbsoluteUri("/");
            var accessToken = K7ServerService.HttpClient.DefaultRequestHeaders.Authorization?.Parameter;

            if (Connectivity.IsOnline)
            {
                string? deviceType = null;
                if (userChanged || K7HubClient.State != HubConnectionState.Connected)
                    deviceType = (await DeviceService.GetDeviceTypeAsync()).ToString();

                K7HubClient.EnsureStartedAsync(baseUri, userId, deviceId, accessToken, deviceName: null, deviceType)
                    .FireAndForget(Logger, "Hub startup failed");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hub startup failed");
        }
    }

    private async Task BindFeedHubAsync()
    {
        if (string.IsNullOrEmpty(_sessionUserId))
        {
            FeedHub.SetEnabled(false);
            return;
        }

        var hubDeviceType = await DeviceService.GetDeviceTypeAsync();
        FeedHub.SetEnabled(true);
        if (hubDeviceType is DeviceType.Phone or DeviceType.Tablet)
            FeedHub.SetMountLimit(3);
        else
            FeedHub.SetMountLimit(null);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppReadySignal.Signal();
            PlaybackAssetLoader.Prefetch(JS);
            await JS.InvokeVoidAsync("K7.applyTheme", ThemeService.Theme.CssDataAttribute);
            await JS.InvokeVoidAsync("K7.dismissPreload");
            _selfRef = DotNetObjectReference.Create(this);
            try
            {
                await SpatialNav.RegisterHomeEscapeAsync(_selfRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }

            try
            {
                await SoftKeyboardBridge.RegisterAsync(JS);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
                Logger.LogDebug(ex, "Soft keyboard bridge registration failed");
            }

            // No-op on non-Windows hosts; Windows MAUI registers the VHS xhr bridge.
            try
            {
                await WindowsStreamFetchBridge.RegisterAsync(JS);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
                Logger.LogDebug(ex, "Windows stream fetch bridge registration failed");
            }

            _firstRenderDone = true;
            RequestOfflineStatsSync();
        }

        if (_showOverlay && !_reconnectAnimationPlayed)
        {
            _reconnectAnimationPlayed = true;
            try
            {
                await JS.InvokeVoidAsync("K7.Lottie.play", _reconnectAnimationContainer,
                    "_content/K7.Clients.Shared.UI/animations/splash.json");
            }
            catch (JSException) { }
        }
    }

    private void RequestOfflineStatsSync() =>
        PlaybackSync.SyncPendingEventsAsync().FireAndForget(Logger, "Offline playback sync failed");

    [JSInvokable]
    public void OnHomeEscapeFirst()
    {
        InvokeAsync(() =>
        {
            Snackbar.Add("Press Escape again to quit", K7Severity.Normal);
            StateHasChanged();
        });
    }

    [JSInvokable]
    public void OnHomeEscapeSecond()
    {
        if (DeviceService.GetClientType() != ClientType.Web)
            AppExitService.Exit();
    }

    private void OnThemeChanged() => OnThemeChangedAsync().FireAndForget(Logger);

    private async Task OnThemeChangedAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.applyTheme", ThemeService.Theme.CssDataAttribute);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }

        await InvokeAsync(StateHasChanged);
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        if (state == HubConnectionState.Connected)
        {
            _overlayTimer?.Dispose();
            _overlayTimer = null;
            _showOverlay = false;
            _reconnectAnimationPlayed = false;
            InvokeAsync(StateHasChanged);
        }
        else
        {
            _overlayTimer ??= new Timer(_ =>
            {
                _showOverlay = true;
                InvokeAsync(StateHasChanged);
            }, null, OverlayDelay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= OnThemeChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        K7HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        FeedHub.Changed -= OnFeedHubChanged;
        _overlayTimer?.Dispose();
        _selfRef?.Dispose();
        SoftKeyboardBridge.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }
}
