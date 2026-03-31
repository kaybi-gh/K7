using System.Globalization;
using System.Security.Claims;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages;

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
    private string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

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
        _backendUrl = ApiClient.HttpClient.BaseAddress?.AbsoluteUri;

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

    private async Task OnCultureChanged(string culture)
    {
        try
        {
            await K7ServerService.UpdateUserLanguageAsync(culture);
        }
        catch
        {
            // Best effort — server may be unreachable
        }

        await JSRuntime.InvokeVoidAsync("blazorCulture.set", culture);
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            L["WarningTitle"],
            L["ChangeServerUrlWarning"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (result == true)
        {
            //K7ServerService.RemoveRegisteredBackendUrl(); // TODO - How do we manage that?
        }
    }

    private async Task SetPin()
    {
        ClearPinMessages();
        var pin = await ShowPinDialog(L["SetPinDialogTitle"]);
        if (pin is null) return;

        var confirm = await ShowPinDialog(L["ConfirmPinDialogTitle"]);
        if (confirm is null) return;

        if (pin != confirm)
        {
            _pinError = L["PinMismatch"];
            return;
        }

        await SavePin(pin);
    }

    private async Task ChangePin()
    {
        ClearPinMessages();
        var newPin = await ShowPinDialog(L["NewPinDialogTitle"]);
        if (newPin is null) return;

        var confirm = await ShowPinDialog(L["ConfirmNewPinDialogTitle"]);
        if (confirm is null) return;

        if (newPin != confirm)
        {
            _pinError = L["PinMismatch"];
            return;
        }

        await SavePin(newPin);
    }

    private async Task RemovePin()
    {
        ClearPinMessages();
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["RemovePinDialogTitle"],
            L["RemovePinConfirm"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (confirmed != true) return;

        await SavePin(null);
    }

    private async Task SavePin(string? pin)
    {
        if (_currentUserId is null)
        {
            _pinError = L["UserUnknown"];
            return;
        }

        try
        {
            await K7ServerService.UpdateUserPinAsync(_currentUserId.Value, pin);

            if (_identityUserId is not null)
                LocalUserService.SetPin(_identityUserId, pin);

            _hasPin = pin is not null;
            _pinSuccess = pin is not null ? L["PinSetSuccess"] : L["PinRemovedSuccess"];
        }
        catch (Exception ex)
        {
            _pinError = S["ErrorWithDetails", ex.Message];
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
