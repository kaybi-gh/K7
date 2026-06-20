using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminVideoPlaybackPanel
{
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
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
            _settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                        ?? new VideoPlayerSettingsDto();
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync()
                           ?? new TrackSelectionPreferencesDto();
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
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync(_selectedLibraryId)
                           ?? new TrackSelectionPreferencesDto();
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

        await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_settings);
    }

    private async Task ResetVideoSettingsAsync()
    {
        await ServerPreferencesService.DeleteServerVideoPlayerSettingsAsync();
        _settings = new VideoPlayerSettingsDto();
    }

    private async Task SaveTrackPreferencesAsync()
    {
        if (_preferences is null)
            return;

        try
        {
            await ServerPreferencesService.UpdateServerTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId);
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
            await ServerPreferencesService.DeleteServerTrackSelectionPreferencesAsync(_selectedLibraryId);
            _preferences = new TrackSelectionPreferencesDto();
            Snackbar.Add(Lt["ResetSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }
}
