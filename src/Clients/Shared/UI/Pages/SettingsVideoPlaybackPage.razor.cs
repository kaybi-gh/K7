using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsVideoPlaybackPage
{
    private sealed record VideoFormState(
        VideoPlayerSettingsDto Settings,
        VideoPlaybackPolicySettingsDto Policy,
        TrackSelectionPreferencesDto Preferences,
        Guid? LibraryId);

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private VideoPlayerSettingsDto? _settings;
    private VideoPlaybackPolicySettingsDto? _videoPolicy;
    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _loading = true;
    private bool _saving;
    private bool _hasUserOverride;
    private readonly SettingsFormTracker<VideoFormState> _formTracker = new();

    private bool IsDirty =>
        _settings is not null
        && _preferences is not null
        && _videoPolicy is not null
        && _formTracker.IsDirty(GetFormState());

    private bool ResetDisabled => !IsDirty && !_hasUserOverride;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _videoPolicy = await UserPreferencesService.GetEffectiveVideoPlaybackPolicySettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
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

    private void OnSubtitleBackgroundOpacityChanged(double value)
    {
        if (_settings is null)
            return;

        _settings.SubtitleBackgroundOpacity = value;
        StateHasChanged();
    }

    private void OnSubtitleShadowBlurChanged(double value)
    {
        if (_settings is null)
            return;

        _settings.SubtitleShadowBlur = value;
        StateHasChanged();
    }

    private static string FormatOpacity(double value) => $"{value:P0}";

    private static string FormatBlur(double value) => $"{value:F1} px";

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
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
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
                UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_settings),
                UserPreferencesService.UpdateUserVideoPlaybackPolicySettingsAsync(_videoPolicy),
                UserPreferencesService.UpdateUserTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId));
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
                UserPreferencesService.ResetUserVideoPlayerSettingsAsync(),
                UserPreferencesService.ResetUserVideoPlaybackPolicySettingsAsync(),
                UserPreferencesService.ResetUserTrackSelectionPreferencesAsync(_selectedLibraryId));
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _videoPolicy = await UserPreferencesService.GetEffectiveVideoPlaybackPolicySettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
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
        _hasUserOverride = await UserPreferenceOverrideHelper.HasVideoOverridesAsync(
            UserPreferencesService,
            _selectedLibraryId);
}
