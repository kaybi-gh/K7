using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Clients.Shared.UI.Pages.Dialogs;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsExternalClientsPage
{
    private List<ClientAppPasswordDto> _passwords = [];
    private bool _loading = true;
    private string _serverUrl = string.Empty;
    private string? _username;
    private string? _createdPassword;

    protected override async Task OnInitializedAsync()
    {
        _serverUrl = NavigationManager.BaseUri.TrimEnd('/');

        try
        {
            var currentUser = await UserAdminService.GetCurrentUserAsync();
            _username = currentUser?.UserName;
        }
        catch
        {
            _username = null;
        }

        await LoadPasswords();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || string.IsNullOrWhiteSpace(_serverUrl))
            return;

        try
        {
            await JS.InvokeVoidAsync("k7QrCode.generate", "opensubsonic-server-qr", _serverUrl, 180);
        }
        catch
        {
            // QR optional
        }
    }

    private async Task LoadPasswords()
    {
        _loading = true;
        try
        {
            _passwords = await ClientAppPasswordUserService.GetClientAppPasswordsAsync();
        }
        catch
        {
            _passwords = [];
        }

        _loading = false;
    }

    private async Task ShowCreateDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateClientAppPasswordDialog>(L["CreatePassword"], null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: CreateClientAppPasswordResponse created })
        {
            _createdPassword = created.Password;
            await LoadPasswords();
        }
    }

    private async Task RevokePassword(ClientAppPasswordDto item)
    {
        var parameters = new K7DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, item.Name }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>(L["RevokeConfirmTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await ClientAppPasswordUserService.RevokeClientAppPasswordAsync(item.Id);
                await LoadPasswords();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private async Task CopyServerUrl()
    {
        await JS.InvokeVoidAsync("K7.shareOrCopy", _serverUrl);
        Snackbar.Add(L["Copied"], K7Severity.Success);
    }

    private async Task CopyUsername()
    {
        if (string.IsNullOrWhiteSpace(_username))
            return;

        await JS.InvokeVoidAsync("K7.shareOrCopy", _username);
        Snackbar.Add(L["Copied"], K7Severity.Success);
    }

    private async Task CopyCreatedPassword()
    {
        if (_createdPassword is null)
            return;

        await JS.InvokeVoidAsync("K7.shareOrCopy", _createdPassword);
        Snackbar.Add(L["Copied"], K7Severity.Success);
    }
}
