using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsVideoPlaybackPage
{
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;

    private VideoPlayerSettingsDto? _settings;
    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
        }
        catch
        {
            _settings = new VideoPlayerSettingsDto();
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

    private async Task SaveVideoSettingsAsync()
    {
        if (_settings is null)
            return;

        await UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_settings);
    }

    private async Task ResetVideoSettingsAsync()
    {
        await UserPreferencesService.ResetUserVideoPlayerSettingsAsync();
        _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
    }

    private async Task SaveTrackPreferencesAsync()
    {
        if (_preferences is null)
            return;

        try
        {
            await UserPreferencesService.UpdateUserTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId);
            Snackbar.Add(Lt["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task ResetTrackPreferencesAsync()
    {
        try
        {
            await UserPreferencesService.ResetUserTrackSelectionPreferencesAsync(_selectedLibraryId);
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
            Snackbar.Add(Lt["ResetSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }
}
