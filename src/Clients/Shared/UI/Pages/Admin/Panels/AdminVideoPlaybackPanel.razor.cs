using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminVideoPlaybackPanel
{
    private sealed record VideoFormState(
        VideoPlayerSettingsDto Settings,
        VideoPlaybackPolicySettingsDto Policy,
        TrackSelectionPreferencesDto Preferences,
        Guid? LibraryId);

    private const string BasePath = "/admin/video-playback";

    /// <summary>Sub-page to show. Null shows the index (plus the first sub-page on wide screens).</summary>
    [Parameter] public string? Section { get; set; }

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private VideoPlayerSettingsDto? _settings;
    private VideoPlaybackPolicySettingsDto? _videoPolicy;
    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _loading = true;
    private bool _saving;
    private bool _hasServerOverride;
    private readonly SettingsFormTracker<VideoFormState> _formTracker = new();

    private IReadOnlyList<SettingsSection> Sections =>
    [
        new(VideoSettingsSections.Tracks, Phosphor.Translate, Lt["SectionTitle"], Ls["TracksSectionDescription"]),
        new(VideoSettingsSections.Seekbar, Phosphor.FastForward, L["SeekbarSection"], Ls["SeekbarSectionDescription"]),
        new(VideoSettingsSections.Skip, Phosphor.SkipForward, L["IntroSkipSection"], Ls["IntroSkipSectionDescription"]),
        new(VideoSettingsSections.NextEpisode, Phosphor.Queue, L["NextEpisodeSection"], Ls["NextEpisodeSectionDescription"]),
        new(VideoSettingsSections.Trailers, Phosphor.FilmStrip, L["TrailersSection"], Ls["TrailersSectionDescription"]),
        new(VideoSettingsSections.Subtitles, Phosphor.ClosedCaptioning, L["SubtitleSection"], Ls["SubtitleSectionDescription"]),
        new(VideoSettingsSections.Resume, Phosphor.ClockCounterClockwise, Ls["ResumeSectionTitle"], Ls["ResumeSectionDescription"])
    ];

    private bool IsSectionOpen => Sections.Any(s => s.Key == Section);

    private string ActiveSection => IsSectionOpen ? Section! : VideoSettingsSections.Tracks;

    private string ActiveSectionTitle => Sections.First(s => s.Key == ActiveSection).Title;

    private bool IsDirty =>
        _settings is not null
        && _preferences is not null
        && _videoPolicy is not null
        && _formTracker.IsDirty(GetFormState());

    private bool ResetDisabled => !IsDirty && !_hasServerOverride;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                        ?? new VideoPlayerSettingsDto();
            _videoPolicy = await ServerPreferencesService.GetServerVideoPlaybackPolicySettingsAsync()
                           ?? new VideoPlaybackPolicySettingsDto();
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync()
                           ?? new TrackSelectionPreferencesDto();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        catch
        {
            _settings = new VideoPlayerSettingsDto();
            _videoPolicy = new VideoPlaybackPolicySettingsDto();
            _preferences = new TrackSelectionPreferencesDto();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }

        _loading = false;
    }

    private VideoFormState GetFormState() =>
        new(_settings!, _videoPolicy!, _preferences!, _selectedLibraryId);

    private void CaptureFormState()
    {
        if (_settings is null || _preferences is null || _videoPolicy is null)
            return;

        _formTracker.Capture(GetFormState());
    }

    private void CancelChanges()
    {
        if (_settings is null || _preferences is null || _videoPolicy is null)
            return;

        var state = _formTracker.Restore();
        _settings = state.Settings;
        _videoPolicy = state.Policy;
        _preferences = state.Preferences;
        _selectedLibraryId = state.LibraryId;
    }

    private void OnVideoPolicyChanged(VideoPlaybackPolicySettingsDto value)
    {
        _videoPolicy = value;
        StateHasChanged();
    }

    private async Task OnLibraryScopeChanged(Guid? libraryId)
    {
        if (libraryId == _selectedLibraryId)
            return;

        if (IsDirty)
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                S["UnsavedChangesTitle"],
                S["UnsavedChangesMessage"],
                yesText: S["Continue"],
                cancelText: S["Cancel"]);

            if (confirmed is not true)
            {
                StateHasChanged();
                return;
            }

            CancelChanges();
        }

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

        CaptureFormState();
        await RefreshOverrideStateAsync();
    }

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _preferences is null || _videoPolicy is null)
            return;

        _saving = true;
        try
        {
            await Task.WhenAll(
                ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_settings),
                ServerPreferencesService.UpdateServerVideoPlaybackPolicySettingsAsync(_videoPolicy),
                ServerPreferencesService.UpdateServerTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId));
            CaptureFormState();
            await RefreshOverrideStateAsync();
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
                ServerPreferencesService.DeleteServerVideoPlaybackPolicySettingsAsync(),
                ServerPreferencesService.DeleteServerTrackSelectionPreferencesAsync(_selectedLibraryId));
            _settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                        ?? new VideoPlayerSettingsDto();
            _videoPolicy = await ServerPreferencesService.GetServerVideoPlaybackPolicySettingsAsync()
                           ?? new VideoPlaybackPolicySettingsDto();
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync(_selectedLibraryId)
                           ?? new TrackSelectionPreferencesDto();
            CaptureFormState();
            await RefreshOverrideStateAsync();
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

    private async Task RefreshOverrideStateAsync() =>
        _hasServerOverride = await ServerPreferenceOverrideHelper.HasVideoOverridesAsync(
            ServerPreferencesService,
            _selectedLibraryId);
}
