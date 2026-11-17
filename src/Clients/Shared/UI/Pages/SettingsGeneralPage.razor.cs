using System.Globalization;
using K7.Clients.Shared.Interfaces;
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
    private readonly string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private string _serverDefaultLanguage = "en";
    private bool _serverDefaultPlayThemeSongs = true;
    private bool _hasLanguageOverride;
    private bool _resetting;
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _disableThemeSongsOnDevice;
    private bool _themeSongsSaving;
    private AppearanceSettingsPanel? _appearancePanel;

    /// <summary>Media pages, with the theme songs that play on them. The former theme songs address lands here.</summary>
    private bool IsMediaPages => ActiveSection == InterfaceSettingsSections.MediaPages;

    private string ActiveSection => InterfaceSettingsSections.Current(NavigationManager) switch
    {
        InterfaceSettingsSections.ThemeSongs or InterfaceSettingsSections.MediaPages => InterfaceSettingsSections.MediaPages,
        InterfaceSettingsSections.AudioPlayer => InterfaceSettingsSections.AudioPlayer,
        InterfaceSettingsSections.VideoPlayer => InterfaceSettingsSections.VideoPlayer,
        _ => InterfaceSettingsSections.Appearance
    };

    /// <summary>Media pages and the two players are parts of the appearance settings, shown by the same panel.</summary>
    private AppearancePanelSection PanelSection => ActiveSection switch
    {
        InterfaceSettingsSections.MediaPages => AppearancePanelSection.MediaPages,
        InterfaceSettingsSections.AudioPlayer => AppearancePanelSection.AudioPlayer,
        InterfaceSettingsSections.VideoPlayer => AppearancePanelSection.VideoPlayer,
        _ => AppearancePanelSection.Look
    };

    private bool AppearanceOnDefaults =>
        (PanelSection != AppearancePanelSection.Look || !_hasLanguageOverride)
        && (_appearancePanel?.IsOnDefaults ?? true);

    /// <summary>The appearance panel saves on its own and reports its state.</summary>
    private SettingsSaveState ActiveSaveState
    {
        get => IsMediaPages && field != SettingsSaveState.Idle
            ? field
            : _appearancePanel?.SaveState ?? SettingsSaveState.Idle;
        set;
    }

    private bool ThemeSongsOnDefaults =>
        _playThemeSongs == _serverDefaultPlayThemeSongs
        && !_disableThemeSongsOnDevice;

    /// <summary>Reset only covers the sub-page on screen.</summary>
    private bool ResetDisabled => AppearanceOnDefaults && (!IsMediaPages || ThemeSongsOnDefaults);

    protected override void OnInitialized()
    {
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
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    // Appearance and Theme songs are the same page: render again when only the address changes.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private void OnAppearanceStateChanged()
    {
        // The panel has news: its state replaces the one a theme song switch left.
        ActiveSaveState = SettingsSaveState.Idle;
        StateHasChanged();
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
            ActiveSaveState = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _playThemeSongs = previous;
            _videoPlayerSettings.PlayThemeSongs = previous;
            ActiveSaveState = SettingsSaveState.Failed;
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
        ActiveSaveState = SettingsSaveState.Saved;
    }

    private async Task ResetToDefaultsAsync()
    {
        if (_resetting || ResetDisabled)
            return;

        _resetting = true;
        ActiveSaveState = SettingsSaveState.Idle;
        try
        {
            if (IsMediaPages && !ThemeSongsOnDefaults)
                await ResetThemeSongsAsync();

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

        if (_appearancePanel is not null)
            await _appearancePanel.ResetAsync();

        _hasLanguageOverride = false;
        ActiveSaveState = SettingsSaveState.Idle;
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
    }
}
