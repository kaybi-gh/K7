using System.Security.Claims;
using K7.Clients.Shared.Components.Dialogs;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using MudBlazor;

namespace K7.Clients.Shared.Pages;

public partial class Settings
{
    private DeviceType _deviceType;
    private List<MediaFormatDto>? _supportedMediaFormats;
    private bool? _hdrSupport;
    private string? _backendUrl;
    private bool _hasPin;
    private Guid? _currentUserId;
    private string? _identityUserId;
    private string? _pinError;
    private string? _pinSuccess;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();
        _supportedMediaFormats = await DeviceService.GetSupportedMediaFormatsAsync();
        _hdrSupport = await DeviceService.GetHdrSupportAsync();
        _backendUrl = K7ServerService.GetAbsoluteUri()?.AbsoluteUri;

        var me = await K7ServerService.GetCurrentUserAsync();
        if (me is not null)
        {
            _currentUserId = me.Id;
            _hasPin = me.HasPin;
        }

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _identityUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? authState.User.FindFirst("sub")?.Value;
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= StateHasChanged;
        ThemeService.DarkModeEnabledOnChange -= StateHasChanged;
    }

    private void ToggleDrawerVariant()
    {
        ThemeService.ToggleDarkMode();
        StateHasChanged();
    }

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            "Warning",
            "Changing K7 server URL will remove all elements related to current K7 server (statistics and files).",
            yesText: "Continue", cancelText: "Cancel");

        if (result == true)
        {
            //K7ServerService.RemoveRegisteredBackendUrl(); // TODO - How do we manage that?
        }
    }

    private async Task SetPin()
    {
        ClearPinMessages();
        var pin = await ShowPinDialog("Set a PIN");
        if (pin is null) return;

        var confirm = await ShowPinDialog("Confirm PIN");
        if (confirm is null) return;

        if (pin != confirm)
        {
            _pinError = "PINs do not match.";
            return;
        }

        await SavePin(pin);
    }

    private async Task ChangePin()
    {
        ClearPinMessages();
        var newPin = await ShowPinDialog("New PIN");
        if (newPin is null) return;

        var confirm = await ShowPinDialog("Confirm new PIN");
        if (confirm is null) return;

        if (newPin != confirm)
        {
            _pinError = "PINs do not match.";
            return;
        }

        await SavePin(newPin);
    }

    private async Task RemovePin()
    {
        ClearPinMessages();
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Remove PIN",
            "Are you sure you want to remove your PIN?",
            yesText: "Remove", cancelText: "Cancel");

        if (confirmed != true) return;

        await SavePin(null);
    }

    private async Task SavePin(string? pin)
    {
        if (_currentUserId is null)
        {
            _pinError = "Unable to identify current user.";
            return;
        }

        try
        {
            await K7ServerService.UpdateUserPinAsync(_currentUserId.Value, pin);

            if (_identityUserId is not null)
                LocalUserService.SetPin(_identityUserId, pin);

            _hasPin = pin is not null;
            _pinSuccess = pin is not null ? "PIN set successfully." : "PIN removed.";
        }
        catch (Exception ex)
        {
            _pinError = $"Failed to update PIN: {ex.Message}";
        }
    }

    private async Task<string?> ShowPinDialog(string title)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PinDialog>(title, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string enteredPin)
            return null;

        return enteredPin;
    }

    private void ClearPinMessages()
    {
        _pinError = null;
        _pinSuccess = null;
    }
}
