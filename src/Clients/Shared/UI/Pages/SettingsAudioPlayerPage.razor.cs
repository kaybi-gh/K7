using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAudioPlayerPage : IDisposable
{
    private sealed record AudioFormState(
        AudioPlayerSettingsDto Settings,
        AudioPlaybackPolicySettingsDto Policy);

    private const string BasePath = "/settings/audio-player";

    private AudioPlayerSettingsDto? _settings;
    private AudioPlaybackPolicySettingsDto? _audioPolicy;
    private bool _loading = true;
    private bool _saving;
    private bool _hasUserOverride;
    private readonly SettingsFormTracker<AudioFormState> _formTracker = new();
    private readonly SettingsAutoSave _autoSave = new();
    private SettingsSaveState _saveStatus;
    private bool _disposed;

    /// <summary>Sub-page to show. Null shows the index (plus the first sub-page on wide screens).</summary>
    [Parameter] public string? Section { get; set; }

    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private MusicSessionPersistenceService MusicSessions { get; set; } = default!;

    private IReadOnlyList<SettingsSection> Sections => AudioSettingsSections.Build(L);

    private bool IsSectionOpen => Sections.Any(s => s.Key == Section);

    private string ActiveSection => IsSectionOpen ? Section! : AudioSettingsSections.Loudness;

    private string ActiveSectionTitle => Sections.First(s => s.Key == ActiveSection).Title;

    /// <summary>
    /// Edits waiting for the debounce already count as saving, so the indicator stays visible
    /// from the change to the end of the request instead of flashing.
    /// </summary>
    private SettingsSaveState CurrentSaveState =>
        _saveStatus != SettingsSaveState.Failed && IsDirty ? SettingsSaveState.Saving : _saveStatus;

    private bool IsDirty =>
        _settings is not null
        && _audioPolicy is not null
        && _formTracker.IsDirty(new AudioFormState(_settings, _audioPolicy));

    private bool ResetDisabled => !IsDirty && !_hasUserOverride;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoSave.Dispose();

        // Leaving inside the quiet time: send the last change instead of dropping it.
        if (!_saving && IsDirty)
            PersistAsync().FireAndForget();
    }

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

    protected override void OnAfterRender(bool firstRender)
    {
        if (_loading || _saving || _disposed || _settings is null || _audioPolicy is null)
            return;

        var state = new AudioFormState(_settings, _audioPolicy);
        if (!_formTracker.IsDirty(state))
            return;

        _autoSave.Schedule(_formTracker.Snapshot(state), () => InvokeAsync(SaveAsync));
    }

    private void OnSettingsChanged(AudioPlayerSettingsDto value)
    {
        _settings = value;
        ApplySettingsToRuntime(_settings);
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

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _audioPolicy is null)
            return;

        _autoSave.Cancel();
        _saving = true;
        _saveStatus = SettingsSaveState.Saving;
        StateHasChanged();
        try
        {
            // Snapshot what is sent: edits made while the request is in flight stay dirty
            // and trigger the next save.
            var sentSnapshot = _formTracker.Snapshot(new AudioFormState(_settings, _audioPolicy));
            var rememberSession = _settings.RememberMusicSession != false;
            await PersistAsync();
            await MusicSessions.ApplyRememberSettingAsync(rememberSession);
            _formTracker.CaptureSnapshot(sentSnapshot);
            _autoSave.Reset();
            await RefreshOverrideStateAsync();
            _saveStatus = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _saveStatus = SettingsSaveState.Failed;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
            if (!_disposed)
                StateHasChanged();
        }
    }

    private async Task PersistAsync()
    {
        if (_settings is null || _audioPolicy is null)
            return;

        await Task.WhenAll(
            UserPreferencesService.UpdateUserAudioPlayerSettingsAsync(_settings),
            UserPreferencesService.UpdateUserAudioPlaybackPolicySettingsAsync(_audioPolicy));
    }

    private async Task ConfirmResetAsync()
    {
        if (_saving)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            S["ResetToDefaultsTitle"],
            S["ResetToDefaultsMessage"],
            yesText: S["ResetToDefaults"],
            cancelText: S["Cancel"]);

        if (confirmed is true)
            await ResetAsync();
    }

    private async Task ResetAsync()
    {
        if (_saving)
            return;

        _autoSave.Reset();
        _saveStatus = SettingsSaveState.Idle;
        _saving = true;
        try
        {
            await Task.WhenAll(
                UserPreferencesService.ResetUserAudioPlayerSettingsAsync(),
                UserPreferencesService.ResetUserAudioPlaybackPolicySettingsAsync());
            _settings = await UserPreferencesService.GetEffectiveAudioPlayerSettingsAsync();
            _audioPolicy = await UserPreferencesService.GetEffectiveAudioPlaybackPolicySettingsAsync();
            ApplySettingsToRuntime(_settings);
            await MusicSessions.ApplyRememberSettingAsync(_settings.RememberMusicSession != false);
            CaptureFormState();
            await RefreshOverrideStateAsync();
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

        AudioPlayerService.SetShowFullscreenOnPlay(settings.ShowFullscreenOnPlay);
        AudioPlayerService.SetKeepScreenOn(settings.KeepScreenOn);
        AudioPlayerService.SetSkipBackSeconds(settings.SkipBackSeconds);
        AudioPlayerService.SetSkipForwardSeconds(settings.SkipForwardSeconds);
    }
}
