using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class Welcome : IDisposable
{
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private K7.Clients.Shared.UI.Components.K7Button? _signInButton;
    private bool _loading;
    private bool _guestEnabled;
    private bool _isTv;
    private string? _error;
    private string? _serverInfoError;

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

        var storedJson = DeviceStorage.Get(PreferenceKeys.SERVER_INFO);
        if (!string.IsNullOrEmpty(storedJson))
        {
            try
            {
                var cached = System.Text.Json.JsonSerializer.Deserialize<ServerInfoDto>(storedJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _guestEnabled = cached?.GuestEnabled == true;
            }
            catch { }
        }

        try
        {
            var serverInfo = await K7ServerService.GetServerInfoAsync();
            _guestEnabled = serverInfo?.GuestEnabled == true;

            var freshJson = System.Text.Json.JsonSerializer.Serialize(serverInfo);
            DeviceStorage.Set(PreferenceKeys.SERVER_INFO, freshJson);
        }
        catch (Exception ex)
        {
            if (!_guestEnabled)
                _serverInfoError = string.Format(S["ErrorWithDetails"], ex.Message);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _signInButton is not null)
        {
            await JSRuntime.InvokeVoidAsync("eval", "document.querySelector('.k7-btn')?.focus()");
        }
    }

    private async Task SignInAsync()
    {
        if (_isTv)
        {
            Navigation.NavigateTo("/linkdevice");
            return;
        }

        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            await AuthService.LoginAsync();
            await CheckAuthAndNavigate();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task ContinueAsGuestAsync()
    {
        _loading = true;
        _error = null;
        StateHasChanged();

        try
        {
            await AuthService.LoginAsGuestAsync();
            await CheckAuthAndNavigate();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
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
            _error = L["SignInFailed"];
            _loading = false;
            StateHasChanged();
        }
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
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
