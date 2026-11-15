using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

/// <summary>
/// Personal media card settings. An account that never changed anything follows the default
/// card style of the server; its first change gives it a style of its own, and "reset to
/// defaults" goes back to following the server. Changes show at once and save on their own.
/// </summary>
public partial class SettingsCardsPage : IDisposable
{
    private readonly SettingsFormTracker<AppearancePreferencesDto> _tracker = new();
    private SettingsAutoSaveSession<AppearancePreferencesDto>? _session;
    private AppearancePreferencesDto _preferences = new();

    // What the editor works on: the user's own style, or a copy of the server default to start from.
    private MediaCardPreferencesDto _cards = new();
    private bool _loaded;

    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private SettingsSaveState SaveState => _session?.State ?? SettingsSaveState.Idle;

    private bool IsBusy => _session?.IsBusy ?? false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _preferences = await PreferencesService.GetAppearancePreferencesAsync();
        }
        catch (HttpRequestException)
        {
            // Offline: edit what the app already shows, the save will report the failure.
            _preferences = ThemeService.Preferences;
        }

        _cards = EditableCards();

        _tracker.Capture(_preferences);
        _session = new SettingsAutoSaveSession<AppearancePreferencesDto>(
            _tracker,
            () => _loaded ? _preferences : null,
            () => PreferencesService.UpdateAppearancePreferencesAsync(_preferences),
            InvokeAsync,
            StateHasChanged,
            ex => Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error));
        _loaded = true;
    }

    protected override void OnAfterRender(bool firstRender) => _session?.OnAfterRender();

    public void Dispose() => _session?.Dispose();

    private MediaCardPreferencesDto EditableCards() =>
        _preferences.Cards ?? MediaCardStyles.Copy(ThemeService.ServerDefaultCards ?? new MediaCardPreferencesDto());

    private void OnCardsChanged()
    {
        // From the first change on, the account keeps its own style.
        _preferences.Cards = _cards;
        ThemeService.Update(_preferences);
    }

    private async Task ResetAsync()
    {
        if (_session is null)
            return;

        await _session.RunAsync(async () =>
        {
            _preferences.Cards = null;
            await PreferencesService.UpdateAppearancePreferencesAsync(_preferences);
            _cards = EditableCards();
            _tracker.Capture(_preferences);
            ThemeService.Update(_preferences);
        });
    }
}
