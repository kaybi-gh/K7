using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.UI.Pages;

public partial class Welcome : IDisposable
{
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _loading;
    private bool _guestEnabled;
    private bool _isTv;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var authProvider = (AuthenticationStateProvider)AuthService;
        authProvider.AuthenticationStateChanged += OnAuthStateChanged;

        var authState = await authProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo(ReturnUrl ?? "/");
            return;
        }

        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        var cachedGuest = CachedGuestAccess.TryGetEnabled(DeviceStorage);
        _guestEnabled = cachedGuest == true;
        if (cachedGuest == false && TryBypassWelcomeWhenGuestUnavailable())
            return;

        try
        {
            var serverInfo = await K7ServerService.GetServerInfoAsync();
            if (serverInfo is not null)
            {
                _guestEnabled = serverInfo.GuestEnabled;
                DeviceStorage.Set(PreferenceKeys.SERVER_INFO, System.Text.Json.JsonSerializer.Serialize(serverInfo));
                if (!serverInfo.GuestEnabled && TryBypassWelcomeWhenGuestUnavailable())
                    return;
            }
        }
        catch (Exception ex)
        {
            if (!_guestEnabled)
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Warning);
        }

        AppReadySignal.Signal();
    }

    /// <summary>
    /// TV Sign In is the device-link QR. When Guest is known to be off, welcome would only show Sign In.
    /// </summary>
    private bool TryBypassWelcomeWhenGuestUnavailable()
    {
        if (!_isTv)
            return false;

        Navigation.NavigateTo(string.IsNullOrEmpty(ReturnUrl)
            ? "/linkdevice"
            : $"/linkdevice?returnUrl={Uri.EscapeDataString(ReturnUrl)}", replace: true);
        return true;
    }

    private async Task SignInAsync()
    {
        if (_isTv)
        {
            Navigation.NavigateTo("/linkdevice");
            return;
        }

        _loading = true;
        StateHasChanged();

        try
        {
            await AuthService.LoginAsync();
            await CheckAuthAndNavigate();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task ContinueAsGuestAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            await AuthService.LoginAsGuestAsync();
            await CheckAuthAndNavigate();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task CheckAuthAndNavigate()
    {
        var authState = await ((AuthenticationStateProvider)AuthService).GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo(ReturnUrl ?? "/");
        }
        else
        {
            Snackbar.Add(L["SignInFailed"], K7Severity.Error);
        }
    }

    private void OnAuthStateChanged(Task<AuthenticationState> task) => OnAuthStateChangedAsync(task).FireAndForget();

    private async Task OnAuthStateChangedAsync(Task<AuthenticationState> task)
    {
        var authState = await task;
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            await InvokeAsync(() => Navigation.NavigateTo(ReturnUrl ?? "/"));
        }
    }

    public void Dispose()
    {
        var authProvider = (AuthenticationStateProvider)AuthService;
        authProvider.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
