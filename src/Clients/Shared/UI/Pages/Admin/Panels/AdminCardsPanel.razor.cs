using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

/// <summary>
/// The default media card style of the server, shown to every account that did not set its own.
/// Saved with the action bar, like the other server defaults.
/// </summary>
public partial class AdminCardsPanel
{
    private readonly SettingsFormTracker<MediaCardPreferencesDto> _tracker = new();
    private MediaCardPreferencesDto _cards = new();
    private bool _isLoading = true;
    private bool _saving;

    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool IsDirty => _tracker.IsDirty(_cards);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var serverInfo = await ServerInfoService.GetServerInfoAsync();
            _cards = serverInfo?.DefaultCards ?? new MediaCardPreferencesDto();
        }
        catch (HttpRequestException)
        {
            _cards = new MediaCardPreferencesDto();
        }

        _tracker.Capture(_cards);
        _isLoading = false;
    }

    private void CancelChanges() => _cards = _tracker.Restore();

    private Task SaveAsync() => PersistAsync(_cards, "DefaultSaved");

    private Task ResetAsync() => PersistAsync(new MediaCardPreferencesDto(), "DefaultReset");

    private async Task PersistAsync(MediaCardPreferencesDto cards, string messageKey)
    {
        _saving = true;
        try
        {
            await ServerInfoService.UpdateDefaultCardStyleAsync(cards);
            _cards = cards;
            _tracker.Capture(_cards);

            // The app follows at once for accounts without a style of their own, this one included.
            ThemeService.SetServerDefaultCards(MediaCardStyles.Copy(_cards));
            Snackbar.Add(L[messageKey], K7Severity.Success);
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
}
