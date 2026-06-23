using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminAudioMuseAiPanel
{
    private AudioMuseAiSettingsDto? _settings;
    private bool _loading = true;
    private bool _saving;
    private bool _testing;
    private AudioMuseAiConnectionResultDto? _testResult;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await AudioMuseAiAdminService.GetSettingsAsync();
        }
        catch
        {
            _settings = new AudioMuseAiSettingsDto();
        }

        _loading = false;
    }

    private async Task SaveAsync()
    {
        if (_settings is null)
            return;

        _saving = true;

        try
        {
            await AudioMuseAiAdminService.UpdateSettingsAsync(_settings);
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
        _testing = true;
        _testResult = null;
        StateHasChanged();

        try
        {
            _testResult = await AudioMuseAiAdminService.TestConnectionAsync();
        }
        catch (Exception ex)
        {
            _testResult = new AudioMuseAiConnectionResultDto { Success = false, Error = ex.Message };
        }

        _testing = false;
        StateHasChanged();
    }
}
