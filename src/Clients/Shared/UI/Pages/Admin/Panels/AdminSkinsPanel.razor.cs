using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

/// <summary>
/// The default media pages and players of the server, shown to every account that did not set
/// them up. Same settings as the personal ones (<see cref="AppearanceSettingsPanel"/>), one set
/// for every device, saved on change. The media pages come with the server default of theme
/// songs, in a second panel, like on the personal page.
/// </summary>
public partial class AdminSkinsPanel : IDisposable
{
    private AppearanceSettingsPanel? _panel;
    private bool _resetting;
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _themeSongsLoaded;
    private bool _themeSongsSaving;
    private SettingsSaveState _themeSongsSaveState;

    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    /// <summary>The former theme songs address lands on the media pages, where they are set up.</summary>
    private string ActiveSection => InterfaceSettingsSections.Current(NavigationManager) switch
    {
        InterfaceSettingsSections.AudioPlayer => InterfaceSettingsSections.AudioPlayer,
        InterfaceSettingsSections.VideoPlayer => InterfaceSettingsSections.VideoPlayer,
        _ => InterfaceSettingsSections.MediaPages
    };

    private bool IsMediaPages => ActiveSection == InterfaceSettingsSections.MediaPages;

    private AppearancePanelSection PanelSection => ActiveSection switch
    {
        InterfaceSettingsSections.AudioPlayer => AppearancePanelSection.AudioPlayer,
        InterfaceSettingsSections.VideoPlayer => AppearancePanelSection.VideoPlayer,
        _ => AppearancePanelSection.MediaPages
    };

    private bool ThemeSongsOnDefaults => _playThemeSongs == new VideoPlayerSettingsDto().PlayThemeSongs;

    /// <summary>The state of the last change, whichever panel made it.</summary>
    private SettingsSaveState ActiveSaveState =>
        IsMediaPages && _themeSongsSaveState != SettingsSaveState.Idle
            ? _themeSongsSaveState
            : _panel?.SaveState ?? SettingsSaveState.Idle;

    /// <summary>Reset only covers the sub-page on screen.</summary>
    private bool ResetDisabled => (_panel?.IsOnDefaults ?? true) && (!IsMediaPages || ThemeSongsOnDefaults);

    public void Dispose() => NavigationManager.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        try
        {
            _videoPlayerSettings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                                   ?? new VideoPlayerSettingsDto();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;
            _themeSongsLoaded = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    // The three sub-pages are the same page: render again when only the address changes.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private void OnPanelStateChanged()
    {
        // The panel has news: its state replaces the one the theme song switch left.
        _themeSongsSaveState = SettingsSaveState.Idle;
        StateHasChanged();
    }

    private Task OnPlayThemeSongsChangedAsync(bool enabled) => SavePlayThemeSongsAsync(enabled);

    /// <summary>A single switch: saved as soon as it is flipped, like the settings above it.</summary>
    private async Task SavePlayThemeSongsAsync(bool enabled)
    {
        if (_themeSongsSaving || enabled == _playThemeSongs)
            return;

        _themeSongsSaving = true;
        _playThemeSongs = enabled;
        _themeSongsSaveState = SettingsSaveState.Saving;
        try
        {
            // The other video settings may have changed on their own page since this one loaded.
            _videoPlayerSettings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                                   ?? _videoPlayerSettings;
            _videoPlayerSettings.PlayThemeSongs = enabled;
            await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_videoPlayerSettings);
            _themeSongsSaveState = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _playThemeSongs = !enabled;
            _themeSongsSaveState = SettingsSaveState.Failed;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _themeSongsSaving = false;
        }
    }

    private async Task ResetAsync()
    {
        if (_resetting || _panel is null)
            return;

        _resetting = true;
        try
        {
            if (IsMediaPages && _themeSongsLoaded && !ThemeSongsOnDefaults)
                await SavePlayThemeSongsAsync(new VideoPlayerSettingsDto().PlayThemeSongs);

            await _panel.ResetAsync();
        }
        finally
        {
            _resetting = false;
        }
    }
}
