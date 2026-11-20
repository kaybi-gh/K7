using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

/// <summary>
/// The default media pages and players of the server, shown to every account that did not set
/// them up. Same settings as the personal ones (<see cref="AppearanceSettingsPanel"/>), for
/// every device or for one type of device. The media pages come with the server default of
/// theme songs, in a second panel, like on the personal page. Nothing is saved on change:
/// the defaults reach every account, so they wait for Save, and Cancel drops them.
/// </summary>
public partial class AdminSkinsPanel : IDisposable
{
    private AppearanceSettingsPanel? _panel;
    private bool _saving;
    private bool _playThemeSongs = true;
    private bool _savedPlayThemeSongs = true;
    private bool _themeSongsLoaded;

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

    private bool ThemeSongsDirty => _themeSongsLoaded && _playThemeSongs != _savedPlayThemeSongs;

    private bool ThemeSongsOnDefaults => _playThemeSongs == new VideoPlayerSettingsDto().PlayThemeSongs;

    private bool IsDirty => (_panel?.IsDirty ?? false) || ThemeSongsDirty;

    /// <summary>Reset only covers the sub-page on screen.</summary>
    private bool ResetDisabled => (_panel?.IsOnDefaults ?? true) && (!IsMediaPages || ThemeSongsOnDefaults);

    public void Dispose() => NavigationManager.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        try
        {
            var settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync() ?? new VideoPlayerSettingsDto();
            _playThemeSongs = settings.PlayThemeSongs;
            _savedPlayThemeSongs = settings.PlayThemeSongs;
            _themeSongsLoaded = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    // The three sub-pages are the same page: render again when only the address changes.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private void OnPanelStateChanged() => StateHasChanged();

    private void OnPlayThemeSongsChanged(bool enabled) => _playThemeSongs = enabled;

    private async Task SaveAsync()
    {
        if (_saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            if (ThemeSongsDirty)
            {
                // The other video settings may have changed on their own page since this one loaded.
                var settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync() ?? new VideoPlayerSettingsDto();
                settings.PlayThemeSongs = _playThemeSongs;
                await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(settings);
                _savedPlayThemeSongs = _playThemeSongs;
            }

            // The panel reports its own failures.
            if (_panel is { IsDirty: true })
                await _panel.SaveChangesAsync();

            if (!IsDirty)
                Snackbar.Add(Lg["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private void CancelChanges()
    {
        _playThemeSongs = _savedPlayThemeSongs;
        _panel?.CancelChanges();
    }

    /// <summary>Puts the sub-page on screen back on the defaults. Like any change, it waits for Save.</summary>
    private async Task ResetAsync()
    {
        if (_panel is null)
            return;

        if (IsMediaPages && _themeSongsLoaded)
            _playThemeSongs = new VideoPlayerSettingsDto().PlayThemeSongs;

        await _panel.ResetAsync();
    }
}
