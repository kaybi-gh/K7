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
    private bool _saving;

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

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _preferences is null)
            return;

        _saving = true;
        try
        {
            await Task.WhenAll(
                ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_settings),
                ServerPreferencesService.UpdateServerTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId));
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
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

    private async Task ResetAsync()
    {
        if (_saving)
            return;

        _saving = true;
        try
        {
            await Task.WhenAll(
                ServerPreferencesService.DeleteServerVideoPlayerSettingsAsync(),
                ServerPreferencesService.DeleteServerTrackSelectionPreferencesAsync(_selectedLibraryId));
            _settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                        ?? new VideoPlayerSettingsDto();
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync(_selectedLibraryId)
                           ?? new TrackSelectionPreferencesDto();
            Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
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
