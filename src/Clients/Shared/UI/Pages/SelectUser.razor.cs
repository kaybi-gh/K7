using System.Net.Http;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SelectUser
{
    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private List<LocalUser> _users = [];
    private bool _singleUserMode;
    private bool _loading;
    private bool _isTv;
    private bool _showOfflineButton;
    private LocalUser? _pendingUser;
    private CancellationTokenSource? _offlineTimerCts;

    protected override async Task OnInitializedAsync()
    {
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            Navigation.NavigateTo("/");
            return;
        }

        _users = LocalUserService.GetAll();
        _singleUserMode = LocalUserService.IsSingleUserMode;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        K7.Clients.Shared.Services.AppReadySignal.Signal();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _isTv && !_loading)
        {
            await JSRuntime.InvokeVoidAsync("eval",
                "document.querySelector('.select-user-card.focusable')?.focus()");
        }
    }

    private async Task SelectUserAsync(LocalUser user)
    {
        if (user.PinHash is not null)
        {
            var pinValid = await PromptPinAsync(user);
            if (!pinValid)
                return;
        }

        _loading = true;
        _showOfflineButton = false;
        _pendingUser = user;
        StateHasChanged();

        StartOfflineTimer();

        try
        {
            var success = await AuthService.SwitchToUserAsync(user.RefreshToken);
            CancelOfflineTimer();
            if (success)
            {
                LocalUserService.SetLastActiveId(user.IdentityUserId);
                Navigation.NavigateTo("/");
            }
            else
            {
                LocalUserService.Remove(user.IdentityUserId);
                _users = LocalUserService.GetAll();
                Snackbar.Add(string.Format(L["SessionExpired"], user.UserName), K7Severity.Error);
                _loading = false;
            }
        }
        catch (HttpRequestException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (TaskCanceledException)
        {
            CancelOfflineTimer();
            AuthService.SignInOffline(user);
            LocalUserService.SetLastActiveId(user.IdentityUserId);
            Snackbar.Add(L["ServerUnreachable"], K7Severity.Warning);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            CancelOfflineTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
        }

        StateHasChanged();
    }

    private void StartOfflineTimer()
    {
        _offlineTimerCts?.Cancel();
        _offlineTimerCts = new CancellationTokenSource();
        var token = _offlineTimerCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            if (!token.IsCancellationRequested)
            {
                await InvokeAsync(() =>
                {
                    _showOfflineButton = true;
                    StateHasChanged();
                });
            }
        }, token);
    }

    private void CancelOfflineTimer()
    {
        _offlineTimerCts?.Cancel();
        _offlineTimerCts?.Dispose();
        _offlineTimerCts = null;
    }

    private Task ContinueOfflineAsync()
    {
        if (_pendingUser is null)
            return Task.CompletedTask;

        CancelOfflineTimer();
        AuthService.SignInOffline(_pendingUser);
        LocalUserService.SetLastActiveId(_pendingUser.IdentityUserId);
        Navigation.NavigateTo("/");
        return Task.CompletedTask;
    }

    private async Task<bool> PromptPinAsync(LocalUser user)
    {
        var parameters = new K7DialogParameters<K7.Clients.Shared.UI.Components.Dialogs.PinDialog>
        {
            { x => x.UserName, user.UserName }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<K7.Clients.Shared.UI.Components.Dialogs.PinDialog>(L["EnterPin"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string pin)
            return false;

        if (!LocalUserService.VerifyPin(user.IdentityUserId, pin))
        {
            Snackbar.Add(L["IncorrectPin"], K7Severity.Error);
            return false;
        }

        return true;
    }

    private async Task AddUserAsync()
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

            var authState = await ((AuthenticationStateProvider)AuthService).GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                Navigation.NavigateTo("/");
            }
            else
            {
                _loading = false;
                _users = LocalUserService.GetAll();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
            StateHasChanged();
        }
    }

    private void OnSingleUserModeChanged(bool value)
    {
        _singleUserMode = value;
        LocalUserService.IsSingleUserMode = value;
    }

    private async Task OnUserKeyDown(KeyboardEventArgs e, LocalUser user)
    {
        if (e.Key is "Enter" or " ")
            await SelectUserAsync(user);
    }

    private async Task OnAddUserKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
            await AddUserAsync();
    }

    private static string GetInitial(LocalUser user)
    {
        var name = user.DisplayName ?? user.UserName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }
}
