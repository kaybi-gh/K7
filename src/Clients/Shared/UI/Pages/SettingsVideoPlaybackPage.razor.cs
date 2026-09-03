using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsVideoPlaybackPage
{
    private sealed record VideoFormState(
        VideoPlayerSettingsDto Settings,
        VideoPlaybackPolicySettingsDto Policy,
        TrackSelectionPreferencesDto Preferences,
        Guid? LibraryId,
        bool AudioPassthrough,
        ExoVideoBufferSize ExoBuffer,
        HdmiAutoFrameRateMode HdmiAfr,
        DolbyVisionDecodeMode DvDecode);

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;

    private VideoPlayerSettingsDto? _settings;
    private VideoPlaybackPolicySettingsDto? _videoPolicy;
    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _audioPassthrough = true;
    private ExoVideoBufferSize _exoBuffer = ExoVideoBufferSize.Auto;
    private HdmiAutoFrameRateMode _hdmiAfr = HdmiAutoFrameRateMode.Disabled;
    private HdmiAutoFrameRateMode _hdmiAfrDefault = HdmiAutoFrameRateMode.Disabled;
    private DolbyVisionDecodeMode _dvDecode = DolbyVisionDecodeMode.Native;
    private DolbyVisionDecodeMode _dvDecodeDefault = DolbyVisionDecodeMode.Native;
    private bool _showDeviceAdvanced;
    private bool _showExoBuffer;
    private bool _showHdmiAfr;
    private bool _loading = true;
    private bool _saving;
    private bool _hasUserOverride;
    private readonly SettingsFormTracker<VideoFormState> _formTracker = new();

    private bool IsDirty =>
        _settings is not null
        && _preferences is not null
        && _videoPolicy is not null
        && _formTracker.IsDirty(GetFormState());

    private bool HasDeviceVideoOverride =>
        _showDeviceAdvanced
        && (!_audioPassthrough
            || _exoBuffer != ExoVideoBufferSize.Auto
            || (_showHdmiAfr && _hdmiAfr != _hdmiAfrDefault)
            || (_showHdmiAfr && _dvDecode != _dvDecodeDefault));

    private bool ResetDisabled => !IsDirty && !_hasUserOverride && !HasDeviceVideoOverride;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _videoPolicy = await UserPreferencesService.GetEffectiveVideoPlaybackPolicySettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
            ApplyLocalVideoPlayerSettings(_settings);
            await LoadDeviceVideoExperienceAsync();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        catch
        {
            _settings = new VideoPlayerSettingsDto();
            _videoPolicy = new VideoPlaybackPolicySettingsDto();
            _preferences = new TrackSelectionPreferencesDto();
            ApplyLocalVideoPlayerSettings(_settings);
            await LoadDeviceVideoExperienceAsync();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }

        _loading = false;
    }

    private VideoFormState GetFormState() =>
        new(
            _settings!,
            _videoPolicy!,
            _preferences!,
            _selectedLibraryId,
            _audioPassthrough,
            _exoBuffer,
            _hdmiAfr,
            _dvDecode);

    private async Task LoadDeviceVideoExperienceAsync()
    {
        _showDeviceAdvanced = DeviceService.GetClientType() == ClientType.Native;
        try
        {
            _showExoBuffer = _showDeviceAdvanced
                && await DeviceService.GetOperatingSystemAsync() == OperatingSystem.Android;
        }
        catch
        {
            _showExoBuffer = false;
        }

        if (!_showDeviceAdvanced)
            return;

        var isTelevision = false;
        string? manufacturer = null;
        string? model = null;
        try
        {
            isTelevision = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
            var details = await DeviceService.GetNativeDeviceDetailsAsync();
            manufacturer = details.RawManufacturer;
            model = details.RawModel;
        }
        catch
        {
        }

        _showHdmiAfr = _showExoBuffer
            && (isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model));
        _hdmiAfrDefault = HdmiAutoFrameRatePolicy.DefaultForDevice(
            isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
            manufacturer,
            model);
        _dvDecodeDefault = DolbyVisionDecodePolicy.DefaultForDevice(
            isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
            manufacturer,
            model);

        try
        {
            _audioPassthrough = DeviceStorage.Get(PreferenceKeys.VIDEO_AUDIO_PASSTHROUGH, true);
            _exoBuffer = ExoVideoBufferPolicy.Parse(
                DeviceStorage.Get(PreferenceKeys.VIDEO_EXO_BUFFER, ExoVideoBufferPolicy.Auto));
            _hdmiAfr = _showHdmiAfr
                ? HdmiAutoFrameRatePolicy.Resolve(
                    DeviceStorage.Get(PreferenceKeys.VIDEO_HDMI_AFR, ""),
                    isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
                    manufacturer,
                    model)
                : _hdmiAfrDefault;
            _dvDecode = _showHdmiAfr
                ? DolbyVisionDecodePolicy.Resolve(
                    DeviceStorage.Get(PreferenceKeys.VIDEO_DV_DECODE, ""),
                    isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
                    manufacturer,
                    model)
                : _dvDecodeDefault;
        }
        catch
        {
            _audioPassthrough = true;
            _exoBuffer = ExoVideoBufferSize.Auto;
            _hdmiAfr = _hdmiAfrDefault;
            _dvDecode = _dvDecodeDefault;
        }
    }

    private void PersistDeviceVideoExperience()
    {
        if (!_showDeviceAdvanced)
            return;

        DeviceStorage.Set(PreferenceKeys.VIDEO_AUDIO_PASSTHROUGH, _audioPassthrough);
        DeviceStorage.Set(PreferenceKeys.VIDEO_EXO_BUFFER, ExoVideoBufferPolicy.Persist(_exoBuffer));
        if (_showHdmiAfr)
        {
            DeviceStorage.Set(PreferenceKeys.VIDEO_HDMI_AFR, HdmiAutoFrameRatePolicy.Persist(_hdmiAfr));
            DeviceStorage.Set(PreferenceKeys.VIDEO_DV_DECODE, DolbyVisionDecodePolicy.Persist(_dvDecode));
        }
    }

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
        _audioPassthrough = state.AudioPassthrough;
        _exoBuffer = state.ExoBuffer;
        _hdmiAfr = state.HdmiAfr;
        _dvDecode = state.DvDecode;
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

    private static string FormatSkipSeconds(int value) => $"{value}s";

    private void OnSkipBackSecondsChanged(int value)
    {
        if (_settings is null)
            return;

        _settings.SkipBackSeconds = value;
        StateHasChanged();
    }

    private void OnSkipForwardSecondsChanged(int value)
    {
        if (_settings is null)
            return;

        _settings.SkipForwardSeconds = value;
        StateHasChanged();
    }

    private async Task ApplyLocalVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings)
    {
        PlayerService.ApplyVideoPlayerUxSettings(settings);
        await ApplySubtitleStyleAsync(settings);
    }

    private void ApplyLocalVideoPlayerSettings(VideoPlayerSettingsDto settings) =>
        ApplyLocalVideoPlayerSettingsAsync(settings).FireAndForget();

    private async Task ApplySubtitleStyleAsync(VideoPlayerSettingsDto settings)
    {
        var deviceType = DeviceService.CachedDeviceType ?? await DeviceService.GetDeviceTypeAsync();
        await SubtitleStyleApplicator.ApplyAsync(JSRuntime, settings, deviceType);
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
            PersistDeviceVideoExperience();
            await ApplyLocalVideoPlayerSettingsAsync(_settings);
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
            _audioPassthrough = true;
            _exoBuffer = ExoVideoBufferSize.Auto;
            _hdmiAfr = _hdmiAfrDefault;
            _dvDecode = _dvDecodeDefault;
            PersistDeviceVideoExperience();
            await ApplyLocalVideoPlayerSettingsAsync(_settings);
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
