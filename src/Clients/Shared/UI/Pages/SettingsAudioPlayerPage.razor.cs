using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAudioPlayerPage
{
    private sealed record AudioFormState(
        AudioPlayerSettingsDto Settings,
        AudioPlaybackPolicySettingsDto Policy);

    private AudioPlayerSettingsDto? _settings;
    private AudioPlaybackPolicySettingsDto? _audioPolicy;
    private bool _loading = true;
    private bool _saving;
    private bool _hasUserOverride;
    private readonly SettingsFormTracker<AudioFormState> _formTracker = new();

    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;

    private bool IsDirty =>
        _settings is not null
        && _audioPolicy is not null
        && _formTracker.IsDirty(new AudioFormState(_settings, _audioPolicy));

    private bool ResetDisabled => !IsDirty && !_hasUserOverride;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await UserPreferencesService.GetEffectiveAudioPlayerSettingsAsync();
            _audioPolicy = await UserPreferencesService.GetEffectiveAudioPlaybackPolicySettingsAsync();
            ApplySettingsToRuntime(_settings);
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        catch
        {
            _settings = new AudioPlayerSettingsDto();
            _audioPolicy = new AudioPlaybackPolicySettingsDto();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }

        _loading = false;
    }

    private void OnSettingsChanged(AudioPlayerSettingsDto value)
    {
        _settings = value;
        StateHasChanged();
    }

    private void OnAudioPolicyChanged(AudioPlaybackPolicySettingsDto value)
    {
        _audioPolicy = value;
        StateHasChanged();
    }

    private void CaptureFormState()
    {
        if (_settings is null || _audioPolicy is null)
            return;

        _formTracker.Capture(new AudioFormState(_settings, _audioPolicy));
    }

    private void CancelChanges()
    {
        if (_settings is null || _audioPolicy is null)
            return;

        var state = _formTracker.Restore();
        _settings = state.Settings;
        _audioPolicy = state.Policy;
        ApplySettingsToRuntime(_settings);
    }

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _audioPolicy is null)
            return;

        _saving = true;
        try
        {
            await Task.WhenAll(
                UserPreferencesService.UpdateUserAudioPlayerSettingsAsync(_settings),
                UserPreferencesService.UpdateUserAudioPlaybackPolicySettingsAsync(_audioPolicy));
            ApplySettingsToRuntime(_settings);
            CaptureFormState();
            await RefreshOverrideStateAsync();
            Snackbar.Add(L["Saved"], K7Severity.Success);
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
                UserPreferencesService.ResetUserAudioPlayerSettingsAsync(),
                UserPreferencesService.ResetUserAudioPlaybackPolicySettingsAsync());
            _settings = await UserPreferencesService.GetEffectiveAudioPlayerSettingsAsync();
            _audioPolicy = await UserPreferencesService.GetEffectiveAudioPlaybackPolicySettingsAsync();
            ApplySettingsToRuntime(_settings);
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RefreshOverrideStateAsync() =>
        _hasUserOverride = await UserPreferenceOverrideHelper.HasAudioOverridesAsync(UserPreferencesService);

    private void ApplySettingsToRuntime(AudioPlayerSettingsDto settings)
    {
        AudioPlayerService.SetLoudnessEnabled(settings.LoudnessEnabled);
        AudioPlayerService.SetLoudnessTargetLufs(settings.LoudnessTargetLufs);
        AudioPlayerService.SetLoudnessPreampDb(settings.LoudnessPreampDb);
        AudioPlayerService.SetLimiterEnabled(settings.LimiterEnabled);
        AudioPlayerService.SetEqEnabled(settings.EqEnabled);
        AudioPlayerService.SetEqBands(settings.EqBands);
        AudioPlayerService.SetEqPresetName(settings.EqPresetName);
        AudioPlayerService.SetCrossfadeDuration(settings.CrossfadeDuration);

        if (settings.AdaptiveCrossfade != AudioPlayerService.AdaptiveCrossfade)
            AudioPlayerService.ToggleAdaptiveCrossfade();

        AutoplayService.SetEnabled(settings.AutoplayEnabled);

        DeviceStorage.Set(PreferenceKeys.STREAMING_QUALITY_WIFI, settings.StreamingQualityWifi);
        DeviceStorage.Set(PreferenceKeys.STREAMING_QUALITY_MOBILE, settings.StreamingQualityMobile);
        DeviceStorage.Set(PreferenceKeys.DOWNMIX_TO_STEREO, settings.DownmixToStereo);

        AudioPlayerService.SetShowFullscreenOnPlay(settings.ShowFullscreenOnPlay);
        AudioPlayerService.SetKeepScreenOn(settings.KeepScreenOn);
        AudioPlayerService.SetSkipBackSeconds(settings.SkipBackSeconds);
        AudioPlayerService.SetSkipForwardSeconds(settings.SkipForwardSeconds);
    }
}
