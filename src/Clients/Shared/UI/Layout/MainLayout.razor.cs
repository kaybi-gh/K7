using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
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
    [Inject] private ICustomNavStore CustomNavStore { get; set; } = default!;
    [Inject] private MusicSessionPersistenceService MusicSessions { get; set; } = default!;

    private K7ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private bool _reconnectAnimationPlayed;
    private bool _firstRenderDone;
    private Timer? _overlayTimer;
    private DotNetObjectReference<MainLayout>? _selfRef;
    private ElementReference _reconnectAnimationContainer;

    private string? _sessionUserId;
    private bool _showCustomNavBar;
    private bool _chromeReady;
    private MediaCardStyleContext? _cardStyles;

    // The layout re-renders on every appearance change, so this follows the setting.
    private bool PlayerAtBottom => ThemeService.Settings.MiniPlayer == MiniPlayerPlacement.Bottom;

    // The account whose appearance preferences came from the server in this session. From then
    // on every change is copied to the device, and the copy on the device is no longer applied.
    private string? _appearanceUserId;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override void OnInitialized()
    {
        ColdStartTrace.Write("MainLayout-init");
        var deviceStorage = Services.GetRequiredService<IDeviceStorageService>();
        PerfTrace.Initialize(deviceStorage.Get(PreferenceKeys.PERF_TRACE_ENABLED) == true, NavigationManager);

        var lastUserId = deviceStorage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        if (!string.IsNullOrEmpty(lastUserId))
            CustomNavStore.BindUser(lastUserId);

        if (CustomNavStore.IsLoaded)
        {
            UpdateCustomNavBarVisibility();
            _chromeReady = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;
        K7HubClient.AppearancePreferencesUpdated += OnRemoteAppearance;
        _cardStyles = MediaCardStyleContext.Next(
            null,
            ThemeService.EffectiveCards);

        if (DeviceService.GetClientType() == ClientType.Native && System.OperatingSystem.IsWindows())
            WebViewJsBridge.SetRuntime(JS);

        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        RestoreCachedAppearanceAsync().FireAndForget(Logger, "Cached appearance restore failed");

        ThemeBootstrap.InitializeAsync(ThemeService, JS, ServerInfoService)
            .FireAndForget(Logger, "Theme bootstrap failed");
        CultureBootstrap.InitializeAsync(JS, ServerInfoService, NavigationManager)
            .FireAndForget(Logger, "Culture bootstrap failed");

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            K7HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        }

        await EnsureUserSessionAsync();
        if (!_chromeReady && string.IsNullOrEmpty(_sessionUserId))
        {
            UpdateCustomNavBarVisibility();
            _chromeReady = true;
        }

        await BindFeedHubAsync();
        FeedHub.Changed += OnFeedHubChanged;
        CustomNavStore.Changed += OnCustomNavChanged;
        NavigationManager.LocationChanged += OnCustomNavLocationChanged;
        await DeviceService.GetDeviceTypeAsync();
        if (!string.IsNullOrEmpty(_sessionUserId) && !CustomNavStore.IsLoaded)
            await CustomNavStore.EnsureLoadedAsync();
        else if (!string.IsNullOrEmpty(_sessionUserId))
            CustomNavStore.RefreshInBackground();

        UpdateCustomNavBarVisibility();
        _chromeReady = true;
    }

    private void OnFeedHubChanged()
    {
        if (MauiNativeVideoChrome.BackgroundUiPaused)
            return;

        InvokeAsync(StateHasChanged).FireAndForget(Logger);
    }

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
                if (_sessionUserId is not null)
                    CustomNavStore.BindUser(null);

                _sessionUserId = null;
                _appearanceUserId = null;
                return;
            }

            var userChanged = !string.Equals(_sessionUserId, userId, StringComparison.Ordinal);
            _sessionUserId = userId;
            CustomNavStore.BindUser(userId);

            if (userChanged && !CustomNavStore.IsLoaded)
            {
                await CustomNavStore.EnsureLoadedAsync();
                UpdateCustomNavBarVisibility();
            }

            if (userChanged && Connectivity.IsOnline)
            {
                LoadAppearanceAsync(userId).FireAndForget(Logger, "Appearance preferences load failed");

                try
                {
                    await DeviceInitializer.InitializeDeviceAsync(Services, userId);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Device init failed");
                }
            }

            var canReport = await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress);
            AudioProgressTracker.SetCanReport(canReport);

            var deviceStorageService = Services.GetRequiredService<IDeviceStorageService>();
            var deviceId = deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
            var deviceName = deviceStorageService.Get(PreferenceKeys.DEVICE_NAME);

            var baseUri = DeviceService.GetClientType() == ClientType.Web
                ? NavigationManager.ToAbsoluteUri("/")
                : K7ServerService.HttpClient.BaseAddress ?? NavigationManager.ToAbsoluteUri("/");
            var accessToken = K7ServerService.HttpClient.DefaultRequestHeaders.Authorization?.Parameter;

            if (Connectivity.IsOnline)
            {
                var deviceType = (await DeviceService.GetDeviceTypeAsync()).ToString();

                try
                {
                    var createRequest = await DeviceService.GenerateCreateDeviceRequestAsync();
                    if (!string.IsNullOrWhiteSpace(createRequest.DeviceName))
                    {
                        deviceName = createRequest.DeviceName.Trim();
                        deviceStorageService.Set(PreferenceKeys.DEVICE_NAME, deviceName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Could not resolve device name before hub start");
                }

                K7HubClient.EnsureStartedAsync(baseUri, userId, deviceId, accessToken, deviceName, deviceType)
                    .FireAndForget(Logger, "Hub startup failed");
            }

            await MusicSessions.BindAndRestoreAsync(userId);
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
            ColdStartTrace.Write("MainLayout-firstRender");
            FirstPaintSignal.Signal();
            AppReadySignal.Signal();
            PlaybackAssetLoader.Prefetch(JS);
            if (PerfTrace.IsEnabled)
                await PerfTrace.SyncJsAsync(JS);
            AotProfileTour.Start(Services, NavigationManager, JS, InvokeAsync);
            if (ThemeService.IsResolved)
                await ThemeBootstrap.ApplyToDocumentAsync(JS, ThemeService.Css);
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

            try
            {
                await JS.InvokeVoidAsync("K7.bindMusicSessionFlush", _selfRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
                Logger.LogDebug(ex, "Music session flush hook failed");
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

    // Only on a media page: these classes restyle its hero and backdrop, nothing else must match them.
    private string? MediaPageClasses => MediaPageRoutes.KindOf(NavigationManager) is { } kind
        ? SkinClasses.Shell(ThemeService.Settings, ThemeService.DeviceType, kind)
        : null;

    private void OnThemeChanged() => OnThemeChangedAsync().FireAndForget(Logger);

    // Another device of the user changed the appearance: this one follows at once. Not before
    // the preferences of the session were loaded, they would be replaced anyway.
    private void OnRemoteAppearance(AppearancePreferencesDto preferences) =>
        InvokeAsync(() =>
        {
            if (_appearanceUserId is not null && _appearanceUserId == _sessionUserId)
                ThemeService.ApplyRemote(preferences);
        }).FireAndForget(Logger);

    private async Task OnThemeChangedAsync()
    {
        try
        {
            await ThemeBootstrap.ApplyToDocumentAsync(JS, ThemeService.Css);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }

        _cardStyles = MediaCardStyleContext.Next(
            _cardStyles,
            ThemeService.EffectiveCards);

        // Changes made in the settings reach the device copy too, for the next start of the app.
        if (_appearanceUserId is not null && _appearanceUserId == _sessionUserId)
            AppearanceCache.Save(
                Services.GetRequiredService<IDeviceStorageService>(),
                _appearanceUserId,
                ThemeService.Preferences,
                ThemeService.ServerDefaultCards);

        await InvokeAsync(StateHasChanged);
    }

    // The last user of this device is almost always the one opening the app: show their look (card
    // style included) before the session is even checked. The server answer replaces it.
    private async Task RestoreCachedAppearanceAsync()
    {
        var storage = Services.GetRequiredService<IDeviceStorageService>();
        var cached = AppearanceCache.TryLoad(storage, storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID));
        if (cached is null)
            return;

        var deviceType = await DeviceService.GetDeviceTypeAsync();
        if (_appearanceUserId is not null)
            return;

        ThemeService.SetServerDefaultCards(cached.ServerDefaultCards);
        ThemeService.Apply(cached.Preferences, deviceType);
    }

    private async Task LoadAppearanceAsync(string userId)
    {
        await ThemeBootstrap.LoadUserAsync(
            ThemeService,
            JS,
            ServerInfoService,
            Services.GetRequiredService<IUserPreferencesService>(),
            DeviceService);

        _appearanceUserId = userId;
        AppearanceCache.Save(
            Services.GetRequiredService<IDeviceStorageService>(),
            userId,
            ThemeService.Preferences,
            ThemeService.ServerDefaultCards);
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

    [JSInvokable]
    public void FlushMusicSession() => MusicSessions.Flush();

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= OnThemeChanged;
        K7HubClient.AppearancePreferencesUpdated -= OnRemoteAppearance;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        K7HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        FeedHub.Changed -= OnFeedHubChanged;
        CustomNavStore.Changed -= OnCustomNavChanged;
        NavigationManager.LocationChanged -= OnCustomNavLocationChanged;
        _overlayTimer?.Dispose();
        _selfRef?.Dispose();
        SoftKeyboardBridge.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }

    private void OnCustomNavChanged() => InvokeAsync(() =>
    {
        UpdateCustomNavBarVisibility();
        StateHasChanged();
    });

    private void OnCustomNavLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(() =>
    {
        UpdateCustomNavBarVisibility();
        StateHasChanged();
    });

    private void UpdateCustomNavBarVisibility()
    {
        var path = CustomNavPath.From(NavigationManager);
        var device = DeviceService.CachedDeviceType ?? DeviceType.Desktop;
        _showCustomNavBar = CustomNavPath.ShouldShowBar(CustomNavStore.Layout, path, device);
    }
}
