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
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IConnectivityService Connectivity { get; set; } = default!;
    [Inject] private ITvHubHostService TvHubHost { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;

    private K7ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private bool _reconnectAnimationPlayed;
    private Timer? _overlayTimer;
    private DotNetObjectReference<MainLayout>? _selfRef;
    private ElementReference _reconnectAnimationContainer;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;

        try
        {
            await ThemeBootstrap.InitializeAsync(ThemeService, JS, ServerInfoService);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Theme bootstrap failed");
        }

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            K7HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        }

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var isAuth = authState.User.Identity?.IsAuthenticated == true;
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? authState.User.FindFirst("sub")?.Value;

            if (isAuth && !string.IsNullOrEmpty(userId))
            {
                if (Connectivity.IsOnline)
                {
                    await DeviceInitializer.InitializeDeviceAsync(Services, userId);
                }

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
                    var deviceType = (await DeviceService.GetDeviceTypeAsync()).ToString();
                    var request = await DeviceService.GenerateCreateDeviceRequestAsync();
                    var deviceName = request.DeviceName;

                    await K7HubClient.EnsureStartedAsync(baseUri, userId, deviceId, accessToken, deviceName, deviceType);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hub startup failed");
        }

        var isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        TvHubHost.SetEnabled(isTv);
        TvHubHost.Changed += OnTvHubHostChanged;
    }

    private void OnTvHubHostChanged() => InvokeAsync(StateHasChanged).FireAndForget(Logger);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("K7.applyTheme", ThemeService.Theme.CssDataAttribute);
            await JS.InvokeVoidAsync("K7.dismissPreload");
            _selfRef = DotNetObjectReference.Create(this);
            try
            {
                await SpatialNav.RegisterHomeEscapeAsync(_selfRef);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
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
        {
#if MAUI
            Microsoft.Maui.Controls.Application.Current?.Quit();
#endif
        }
    }

    private void OnThemeChanged() => OnThemeChangedAsync().FireAndForget(Logger);

    private async Task OnThemeChangedAsync()
    {
        await JS.InvokeVoidAsync("K7.applyTheme", ThemeService.Theme.CssDataAttribute);
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
        K7HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        TvHubHost.Changed -= OnTvHubHostChanged;
        _overlayTimer?.Dispose();
        _selfRef?.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }
}
