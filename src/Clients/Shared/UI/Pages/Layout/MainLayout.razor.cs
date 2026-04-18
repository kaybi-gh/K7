using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private bool _sidebarOpen;
    private Timer? _overlayTimer;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override async Task OnInitializedAsync()
    {
        _sidebarOpen = SidebarService.IsOpen;
        SidebarService.IsOpenOnChange += OnSidebarStateChanged;

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
        }
    }

    private async void OnThemeChanged()
    {
        await JS.InvokeVoidAsync("K7.applyTheme", ThemeService.Theme.CssDataAttribute);
        await InvokeAsync(StateHasChanged);
    }

    private void OnSidebarStateChanged()
    {
        _sidebarOpen = SidebarService.IsOpen;
        InvokeAsync(StateHasChanged);
    }

    internal void SetSidebarOpen(bool open)
    {
        if (_sidebarOpen != open)
        {
            _sidebarOpen = open;
            InvokeAsync(StateHasChanged);
        }
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
        SidebarService.IsOpenOnChange -= OnSidebarStateChanged;
        ThemeService.ThemeOnChange -= OnThemeChanged;
        K7HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _overlayTimer?.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }
}
