using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsWatchTogetherSyncPlayPage
{
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private bool _savingSyncPlayPreference;
    private SyncPlayPreferencesDto _syncPlayPreferences = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _syncPlayPreferences = await PreferencesService.GetSyncPlayPreferencesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OnSyncPlayInvitationsChanged(bool value)
    {
        _syncPlayPreferences.InvitationsEnabled = value;
        _savingSyncPlayPreference = true;
        try
        {
            await PreferencesService.UpdateSyncPlayPreferencesAsync(_syncPlayPreferences);
            Snackbar.Add(S["Saved"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            _syncPlayPreferences.InvitationsEnabled = !value;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _savingSyncPlayPreference = false;
        }
    }
}
