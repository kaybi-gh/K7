using System.Globalization;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsGeneralPage : IDisposable
{
    private string? _backendUrl;
    private string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private string _serverDefaultLanguage = "en";
    private string _serverDefaultThemeCss = "default-dark";
    private bool _hasLanguageOverride;
    private bool _resetting;

    private bool IsOnDefaults =>
        !_hasLanguageOverride
        && ThemeService.Theme.CssDataAttribute == _serverDefaultThemeCss;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _backendUrl = ApiClient.HttpClient.BaseAddress?.AbsoluteUri;

        try
        {
            var serverInfo = await ServerInfoService.GetServerInfoAsync();
            if (serverInfo is not null)
            {
                _serverDefaultLanguage = serverInfo.DefaultLanguage;
                _serverDefaultThemeCss = serverInfo.DefaultTheme;
            }

            var userLanguage = await UserService.GetUserLanguageAsync();
            _hasLanguageOverride = !string.IsNullOrEmpty(userLanguage);
        }
        catch
        {
            // Best effort
        }
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCultureChanged(string culture)
    {
        try
        {
            await UserService.UpdateUserLanguageAsync(culture);
            _hasLanguageOverride = true;
        }
        catch
        {
            // Best effort
        }

        await JSRuntime.InvokeVoidAsync("blazorCulture.set", culture);
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    private async Task ResetToDefaultsAsync()
    {
        if (_resetting || IsOnDefaults)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            S["ResetToDefaultsTitle"],
            S["ResetToDefaultsMessage"],
            yesText: S["ResetToDefaults"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        _resetting = true;
        try
        {
            var defaultTheme = Themes.FromCssDataAttribute(_serverDefaultThemeCss) ?? Themes.DefaultDark;

            try
            {
                await UserService.DeleteUserLanguageAsync();
            }
            catch
            {
                // Best effort
            }

            await JSRuntime.InvokeVoidAsync("K7.clearSavedTheme");
            ThemeService.Theme = defaultTheme;
            await JSRuntime.InvokeVoidAsync("K7.applyTheme", defaultTheme.CssDataAttribute);

            _hasLanguageOverride = false;
            Snackbar.Add(L["GeneralResetSuccess"], K7Severity.Success);

            await JSRuntime.InvokeVoidAsync("blazorCulture.set", _serverDefaultLanguage);
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
        }
        finally
        {
            _resetting = false;
        }
    }

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            L["WarningTitle"],
            L["ChangeServerUrlWarning"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (result == true)
        {
            ServerConnectionService.DisconnectAndReset();
        }
    }
}
