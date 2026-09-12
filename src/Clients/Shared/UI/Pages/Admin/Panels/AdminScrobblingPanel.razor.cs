using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Scrobbling;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminScrobblingPanel
{
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private ScrobblingSettingsDto? _settings;
    private bool _loading = true;
    private bool _saving;
    private readonly SettingsFormTracker<ScrobblingSettingsDto> _formTracker = new();

    private bool IsDirty =>
        _settings is not null && _formTracker.IsDirty(_settings);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await ScrobblingAdmin.GetSettingsAsync();
        }
        catch
        {
            _settings = new ScrobblingSettingsDto();
        }

        CaptureFormState();
        _loading = false;
    }

    private void CaptureFormState()
    {
        if (_settings is null)
            return;

        _formTracker.Capture(_settings);
    }

    private void CancelChanges()
    {
        _settings = _formTracker.Restore();
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (_settings is null)
            return;

        _settings = _settings with { Enabled = enabled };
    }

    private void OnLastFmApiKeyChanged(string value) =>
        UpdateSettings(s => s with { LastFmApiKey = value });

    private void OnLastFmApiSecretChanged(string value) =>
        UpdateSettings(s => s with { LastFmApiSecret = value });

    private void OnLastFmApiHostChanged(string value) =>
        UpdateSettings(s => s with { LastFmApiHost = value });

    private void OnTraktClientIdChanged(string value) =>
        UpdateSettings(s => s with { TraktClientId = value });

    private void OnTraktClientSecretChanged(string value) =>
        UpdateSettings(s => s with { TraktClientSecret = value });

    private void UpdateSettings(Func<ScrobblingSettingsDto, ScrobblingSettingsDto> update)
    {
        if (_settings is null)
            return;

        _settings = update(_settings);
    }

    private async Task SaveAsync()
    {
        if (_settings is null || _saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            await ScrobblingAdmin.UpdateSettingsAsync(_settings);
            CaptureFormState();
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
}
