using System.Globalization;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Shared;
using K7.Shared.Dtos;
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
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _disableThemeSongsOnDevice;
    private bool _themeSongsSaving;

    private bool IsOnDefaults =>
        !_hasLanguageOverride
        && ThemeService.Theme.CssDataAttribute == _serverDefaultThemeCss;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;
        _disableThemeSongsOnDevice = DeviceStorageService.Get(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE) == true;
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

        try
        {
            _videoPlayerSettings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;
        }
        catch
        {
            _videoPlayerSettings = new VideoPlayerSettingsDto();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;
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

    private async Task OnPlayThemeSongsChangedAsync(bool enabled)
    {
        if (_themeSongsSaving || enabled == _playThemeSongs)
            return;

        _themeSongsSaving = true;
        var previous = _playThemeSongs;
        _playThemeSongs = enabled;
        try
        {
            _videoPlayerSettings.PlayThemeSongs = enabled;
            await UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_videoPlayerSettings);
        }
        catch (Exception ex)
        {
            _playThemeSongs = previous;
            _videoPlayerSettings.PlayThemeSongs = previous;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _themeSongsSaving = false;
        }
    }

    private void OnDisableThemeSongsOnDeviceChanged(bool disabled)
    {
        _disableThemeSongsOnDevice = disabled;
        DeviceStorageService.Set(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE, disabled);
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
