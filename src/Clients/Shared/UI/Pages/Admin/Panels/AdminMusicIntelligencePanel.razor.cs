using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminMusicIntelligencePanel
{
    private MusicIntelligenceSettingsDto? _settings;
    private bool _loading = true;
    private bool _saving;
    private bool _testing;
    private MusicIntelligenceConnectionResultDto? _testResult;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await MusicIntelligenceAdmin.GetSettingsAsync();
        }
        catch
        {
            _settings = new MusicIntelligenceSettingsDto();
        }

        _loading = false;
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (_settings is null)
            return;

        _settings = _settings with { Enabled = enabled };
        _testResult = null;
    }

    private void OnInstantPlaylistEnabledChanged(bool enabled)
    {
        if (_settings is null)
            return;

        _settings = _settings with { InstantPlaylistEnabled = enabled };
    }

    private async Task SaveAsync()
    {
        if (_settings is null)
            return;

        _saving = true;

        try
        {
            await MusicIntelligenceAdmin.UpdateSettingsAsync(_settings);
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
        }
        catch
        {
            Snackbar.Add(L["SaveError"], K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task TestConnection()
    {
        if (_settings is null)
            return;

        _testing = true;
        _testResult = null;
        StateHasChanged();

        try
        {
            _testResult = await MusicIntelligenceAdmin.TestConnectionAsync(_settings);
        }
        catch (Exception ex)
        {
            _testResult = new MusicIntelligenceConnectionResultDto { Success = false, Error = ex.Message };
        }

        _testing = false;
        StateHasChanged();
    }
}
