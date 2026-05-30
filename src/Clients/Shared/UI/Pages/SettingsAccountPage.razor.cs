using System.Globalization;
using System.Security.Claims;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAccountPage
{
    private const long MaxAvatarSize = 2 * 1024 * 1024;

    private Guid? _currentUserId;
    private string? _identityUserId;

    // Avatar
    private string? _avatarUrl;
    private string _avatarLetter = "";
    private string? _avatarError;
    private InputFile? _fileInput;

    // Profile
    private string? _displayName;
    private string? _originalDisplayName;
    private string? _profileSuccess;
    private string? _profileError;

    // Email
    private string? _email;
    private string? _newEmail;
    private string? _emailPassword;
    private string? _emailError;
    private string? _emailSuccess;

    // Password
    private bool _hasPassword;
    private bool _canRemovePassword;
    private string? _passwordError;
    private string? _passwordSuccess;

    // PIN
    private bool _hasPin;
    private string? _pinError;
    private string? _pinSuccess;

    // Language
    private string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    // Login Methods
    private LoginMethodsDto? _loginMethods;
    private string? _loginMethodsError;

    // Delete
    private string? _deleteError;

    private bool _isGuest;

    protected override async Task OnInitializedAsync()
    {
        var me = await UserService.GetCurrentUserAsync();
        if (me is not null)
        {
            _currentUserId = me.Id;
            _displayName = me.DisplayName ?? me.UserName;
            _originalDisplayName = _displayName;
            _avatarUrl = me.AvatarUrl;
            _avatarLetter = GetLetter(me.DisplayName, me.UserName);
            _hasPin = me.HasPin;
            _email = me.Email;
            _newEmail = me.Email;
            _isGuest = me.IsGuest;
        }

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _identityUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? authState.User.FindFirst("sub")?.Value;

        if (!_isGuest)
        {
            await LoadLoginMethodsAsync();
        }
    }

    private async Task LoadLoginMethodsAsync()
    {
        try
        {
            _loginMethods = await UserService.GetLoginMethodsAsync();
            _hasPassword = _loginMethods.HasPassword;
            _canRemovePassword = _loginMethods.CanRemovePassword;
        }
        catch
        {
            // Login methods may not be available for all users
        }
    }

    private static string GetLetter(string? displayName, string? userName)
    {
        var name = displayName ?? userName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1];
    }

    // Avatar
    private async Task UploadAvatar()
    {
        _avatarError = null;
        await JSRuntime.InvokeVoidAsync("eval", "document.querySelector('input[type=file][accept]').click()");
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        _avatarError = null;
        var file = e.File;

        if (file.Size > MaxAvatarSize)
        {
            _avatarError = L["AvatarTooLarge"];
            return;
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await using (var browserStream = file.OpenReadStream(MaxAvatarSize))
            {
                await browserStream.CopyToAsync(memoryStream);
            }

            memoryStream.Position = 0;
            await UserService.UploadAvatarAsync(memoryStream, file.Name);
            var me = await UserService.GetCurrentUserAsync();
            _avatarUrl = me?.AvatarUrl;
        }
        catch (Exception ex)
        {
            _avatarError = S["ErrorWithDetails", ex.Message];
        }
    }

    private async Task RemoveAvatar()
    {
        _avatarError = null;
        try
        {
            await UserService.RemoveAvatarAsync();
            _avatarUrl = null;
        }
        catch (Exception ex)
        {
            _avatarError = S["ErrorWithDetails", ex.Message];
        }
    }

    // Profile
    private async Task SaveDisplayName()
    {
        _profileError = null;
        _profileSuccess = null;

        try
        {
            await UserService.UpdateProfileAsync(new UpdateProfileRequest { DisplayName = _displayName });
            _originalDisplayName = _displayName;
            _avatarLetter = GetLetter(_displayName, null);
            _profileSuccess = L["ProfileSaved"];
        }
        catch (Exception ex)
        {
            _profileError = S["ErrorWithDetails", ex.Message];
        }
    }

    // Email
    private async Task SaveEmail()
    {
        _emailError = null;
        _emailSuccess = null;

        if (string.IsNullOrWhiteSpace(_newEmail))
        {
            _emailError = L["EmailRequired"];
            return;
        }

        if (string.IsNullOrWhiteSpace(_emailPassword))
        {
            _emailError = L["PasswordRequired"];
            return;
        }

        try
        {
            await UserService.UpdateEmailAsync(new UpdateEmailRequest { Email = _newEmail!, CurrentPassword = _emailPassword! });
            _email = _newEmail;
            _emailPassword = null;
            _emailSuccess = L["EmailSaved"];
        }
        catch (Exception ex)
        {
            _emailError = S["ErrorWithDetails", ex.Message];
        }
    }

    // Password
    private async Task OpenPasswordDialog()
    {
        _passwordError = null;
        _passwordSuccess = null;

        var parameters = new K7DialogParameters<ChangePasswordDialog>
        {
            { x => x.HasPassword, _hasPassword },
            { x => x.CanRemovePassword, _canRemovePassword }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ChangePasswordDialog>(
            _hasPassword ? L["ChangePassword"] : L["SetPassword"], parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return;

        if (result.Data is not PasswordDialogResult dialogResult)
            return;

        try
        {
            switch (dialogResult.Action)
            {
                case PasswordAction.Change:
                    await UserService.ChangePasswordAsync(new ChangePasswordRequest
                    {
                        CurrentPassword = dialogResult.CurrentPassword!,
                        NewPassword = dialogResult.NewPassword!
                    });
                    _passwordSuccess = L["PasswordChanged"];
                    break;

                case PasswordAction.Set:
                    await UserService.SetPasswordAsync(new SetPasswordRequest { NewPassword = dialogResult.NewPassword! });
                    _hasPassword = true;
                    _passwordSuccess = L["PasswordSet"];
                    break;

                case PasswordAction.Remove:
                    var passwordForRemoval = await ShowPasswordConfirmDialog();
                    if (passwordForRemoval is null) return;
                    await UserService.RemovePasswordAsync(new RemovePasswordRequest { CurrentPassword = passwordForRemoval });
                    _hasPassword = false;
                    _passwordSuccess = L["PasswordRemoved"];
                    break;
            }

            await LoadLoginMethodsAsync();
        }
        catch (Exception ex)
        {
            _passwordError = S["ErrorWithDetails", ex.Message];
        }
    }
    private async Task<string?> ShowPasswordConfirmDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PasswordConfirmDialog>(L["ConfirmPassword"], null, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return null;

        return result.Data as string;
    }

    // PIN
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
            await UserService.UpdateUserPinAsync(_currentUserId.Value, pin);

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
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<PinDialog>(title, null, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return null;

        return result.Data as string;
    }

    private void ClearPinMessages()
    {
        _pinError = null;
        _pinSuccess = null;
    }

    // Language
    private async Task OnCultureChanged(string culture)
    {
        try
        {
            await UserService.UpdateUserLanguageAsync(culture);
        }
        catch
        {
            // Best effort
        }

        await JSRuntime.InvokeVoidAsync("blazorCulture.set", culture);
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    // Login Methods
    private async Task UnlinkLogin(string provider)
    {
        _loginMethodsError = null;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["UnlinkLoginTitle"],
            L["UnlinkLoginConfirm"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (confirmed != true) return;

        try
        {
            await UserService.UnlinkExternalLoginAsync(provider);
            await LoadLoginMethodsAsync();
        }
        catch (Exception ex)
        {
            _loginMethodsError = S["ErrorWithDetails", ex.Message];
        }
    }

    // Delete Account
    private async Task DeleteAccount()
    {
        _deleteError = null;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteAccountTitle"],
            L["DeleteAccountConfirm"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (confirmed != true) return;

        string? password = null;
        if (_hasPassword)
        {
            password = await ShowPasswordConfirmDialog();
            if (password is null) return;
        }

        try
        {
            await UserService.DeleteAccountAsync(new DeleteAccountRequest { CurrentPassword = password });
            NavigationManager.NavigateTo("/", forceLoad: true);
        }
        catch (Exception ex)
        {
            _deleteError = S["ErrorWithDetails", ex.Message];
        }
    }
}
