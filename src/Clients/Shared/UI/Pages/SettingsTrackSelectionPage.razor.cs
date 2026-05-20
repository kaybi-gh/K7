using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsTrackSelectionPage
{
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
        }
        catch
        {
            _preferences = new TrackSelectionPreferencesDto();
        }

        _loading = false;
    }

    private async Task OnLibraryScopeChanged(Guid? libraryId)
    {
        _selectedLibraryId = libraryId;

        try
        {
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
        }
        catch
        {
            _preferences = new TrackSelectionPreferencesDto();
        }
    }

    private async Task SaveAsync()
    {
        if (_preferences is null)
            return;

        try
        {
            await UserPreferencesService.UpdateUserTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId);
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            await UserPreferencesService.ResetUserTrackSelectionPreferencesAsync(_selectedLibraryId);
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
            Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }
}
