using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Clients.Shared.UI.Components;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsGeneralPage : IDisposable
{
    private string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private string _serverDefaultLanguage = "en";
    private string _serverDefaultThemeCss = "default-dark";
    private bool _serverDefaultPlayThemeSongs = true;
    private bool _hasLanguageOverride;
    private bool _resetting;
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _disableThemeSongsOnDevice;
    private bool _themeSongsSaving;
    private SettingsSaveState _saveState;

    private bool IsThemeSongs => InterfaceSettingsSections.IsThemeSongs(NavigationManager);

    private string ActiveSection =>
        IsThemeSongs ? InterfaceSettingsSections.ThemeSongs : InterfaceSettingsSections.Appearance;

    private bool AppearanceOnDefaults =>
        !_hasLanguageOverride
        && ThemeService.Theme.CssDataAttribute == _serverDefaultThemeCss;

    private bool ThemeSongsOnDefaults =>
        _playThemeSongs == _serverDefaultPlayThemeSongs
        && !_disableThemeSongsOnDevice;

    /// <summary>Reset only covers the sub-page on screen.</summary>
    private bool ResetDisabled => IsThemeSongs ? ThemeSongsOnDefaults : AppearanceOnDefaults;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += OnThemeChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
        _disableThemeSongsOnDevice = DeviceStorageService.Get(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE) == true;
    }

    protected override async Task OnInitializedAsync()
    {
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
            var serverVideoSettings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync();
            _serverDefaultPlayThemeSongs = serverVideoSettings?.PlayThemeSongs ?? true;

            _videoPlayerSettings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;
        }
        catch
        {
            _videoPlayerSettings = new VideoPlayerSettingsDto();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;
            _serverDefaultPlayThemeSongs = true;
        }
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= OnThemeChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    // Appearance and Theme songs are the same page: render again when only the address changes.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private void OnThemeChanged()
    {
        _saveState = SettingsSaveState.Saved;
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
            _saveState = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _playThemeSongs = previous;
            _videoPlayerSettings.PlayThemeSongs = previous;
            _saveState = SettingsSaveState.Failed;
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
        _saveState = SettingsSaveState.Saved;
    }

    private async Task ResetToDefaultsAsync()
    {
        if (_resetting || ResetDisabled)
            return;

        _resetting = true;
        _saveState = SettingsSaveState.Idle;
        try
        {
            if (IsThemeSongs)
                await ResetThemeSongsAsync();
            else
                await ResetAppearanceAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _resetting = false;
        }
    }

    private async Task ResetAppearanceAsync()
    {
        var defaultTheme = Themes.FromCssDataAttribute(_serverDefaultThemeCss) ?? Themes.DefaultDark;
        var needsCultureReload = _hasLanguageOverride
            || !string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                _serverDefaultLanguage,
                StringComparison.OrdinalIgnoreCase);

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
        _saveState = SettingsSaveState.Idle;
        Snackbar.Add(L["GeneralResetSuccess"], K7Severity.Success);

        if (needsCultureReload)
        {
            await JSRuntime.InvokeVoidAsync("blazorCulture.set", _serverDefaultLanguage);
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
        }
    }

    private async Task ResetThemeSongsAsync()
    {
        if (_disableThemeSongsOnDevice)
        {
            _disableThemeSongsOnDevice = false;
            DeviceStorageService.Remove(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE);
        }

        if (_playThemeSongs != _serverDefaultPlayThemeSongs)
        {
            _playThemeSongs = _serverDefaultPlayThemeSongs;
            _videoPlayerSettings.PlayThemeSongs = _serverDefaultPlayThemeSongs;
            await UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_videoPlayerSettings);
        }

        Snackbar.Add(L["GeneralResetSuccess"], K7Severity.Success);
    }
}
