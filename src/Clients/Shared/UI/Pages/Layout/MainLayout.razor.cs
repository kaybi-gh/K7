using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.UI.Pages.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    private ErrorBoundary? _errorBoundary;
    private bool _showOverlay;
    private Timer? _overlayTimer;

    private static readonly TimeSpan OverlayDelay = TimeSpan.FromSeconds(3);

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;

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

                var baseUri = NavigationManager.ToAbsoluteUri("/");
                await K7HubClient.EnsureStartedAsync(baseUri, userId, deviceId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainLayout] Hub startup failed: {ex}");
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
        ThemeService.ThemeOnChange -= StateHasChanged;
        ThemeService.DarkModeEnabledOnChange -= StateHasChanged;
        K7HubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _overlayTimer?.Dispose();
    }

    private void Recover()
    {
        _errorBoundary?.Recover();
    }
}
