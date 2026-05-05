using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private K7ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private Timer? _overlayTimer;
    private DotNetObjectReference<MainLayout>? _selfRef;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            K7HubClient.ConnectionStateChanged += OnConnectionStateChanged;
        }

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var isAuth = authState.User.Identity?.IsAuthenticated == true;
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (isAuth && !string.IsNullOrEmpty(userId))
            {
                await DeviceInitializer.InitializeDeviceAsync(Services, userId);

                var canReport = await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress);
                AudioProgressTracker.SetCanReport(canReport);

                var deviceStorageService = Services.GetRequiredService<IDeviceStorageService>();
                var deviceId = deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

                var baseUri = DeviceService.GetClientType() == ClientType.Web
                    ? NavigationManager.ToAbsoluteUri("/")
                    : K7ServerService.HttpClient.BaseAddress ?? NavigationManager.ToAbsoluteUri("/");
                var accessToken = K7ServerService.HttpClient.DefaultRequestHeaders.Authorization?.Parameter;
                await K7HubClient.EnsureStartedAsync(baseUri, userId, deviceId, accessToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainLayout] Hub startup failed: {ex}");
        }
    }

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

    private async void OnThemeChanged()
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
        _overlayTimer?.Dispose();
        _selfRef?.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }
}
