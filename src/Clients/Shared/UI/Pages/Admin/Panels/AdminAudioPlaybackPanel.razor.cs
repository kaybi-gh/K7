using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminAudioPlaybackPanel
{
    private sealed record AudioFormState(
        AudioPlayerSettingsDto Settings,
        AudioPlaybackPolicySettingsDto Policy);

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;

    private AudioPlayerSettingsDto? _settings;
    private AudioPlaybackPolicySettingsDto? _audioPolicy;
    private bool _loading = true;
    private bool _saving;
    private bool _hasServerOverride;
    private readonly SettingsFormTracker<AudioFormState> _formTracker = new();

    private bool IsDirty =>
        _settings is not null
        && _audioPolicy is not null
        && _formTracker.IsDirty(new AudioFormState(_settings, _audioPolicy));

    private bool ResetDisabled => !IsDirty && !_hasServerOverride;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await ServerPreferencesService.GetServerAudioPlayerSettingsAsync()
                        ?? new AudioPlayerSettingsDto();
            _audioPolicy = await ServerPreferencesService.GetServerAudioPlaybackPolicySettingsAsync()
                           ?? new AudioPlaybackPolicySettingsDto();
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
    }

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _audioPolicy is null)
            return;

        _saving = true;
        try
        {
            await Task.WhenAll(
                ServerPreferencesService.UpdateServerAudioPlayerSettingsAsync(_settings),
                ServerPreferencesService.UpdateServerAudioPlaybackPolicySettingsAsync(_audioPolicy));
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
                ServerPreferencesService.DeleteServerAudioPlayerSettingsAsync(),
                ServerPreferencesService.DeleteServerAudioPlaybackPolicySettingsAsync());
            _settings = await ServerPreferencesService.GetServerAudioPlayerSettingsAsync()
                        ?? new AudioPlayerSettingsDto();
            _audioPolicy = await ServerPreferencesService.GetServerAudioPlaybackPolicySettingsAsync()
                           ?? new AudioPlaybackPolicySettingsDto();
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
        _hasServerOverride = await ServerPreferenceOverrideHelper.HasAudioOverridesAsync(ServerPreferencesService);
}
