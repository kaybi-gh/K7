using K7.Shared.Dtos;
using K7.Shared.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminTranscodingPanel
{
    private TranscodeSettingsDto? _settings;
    private FfmpegCapabilitiesDto? _capabilities;
    private FfmpegTranscodeTestResultDto? _testResult;
    private bool _loading = true;
    private bool _saving;
    private bool _testing;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await TranscodeAdmin.GetSettingsAsync();
            _capabilities = await TranscodeAdmin.GetCapabilitiesAsync();
        }
        catch
        {
            _settings = new TranscodeSettingsDto();
        }

        _loading = false;
    }

    private void OnEncoderModeChanged(HardwareEncoderMode mode)
    {
        if (_settings is null)
            return;

        _settings = _settings with { EncoderMode = mode };
    }

    private async Task SaveAsync()
    {
        if (_settings is null)
            return;

        _saving = true;

        try
        {
            await TranscodeAdmin.UpdateSettingsAsync(_settings);
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

    private async Task TestEncoderAsync()
    {
        _testing = true;
        _testResult = null;
        StateHasChanged();

        try
        {
            _testResult = await TranscodeAdmin.TestEncoderAsync();
            if (_testResult.Capabilities is not null)
                _capabilities = _testResult.Capabilities;
        }
        catch (Exception ex)
        {
            _testResult = new FfmpegTranscodeTestResultDto { Success = false, Error = ex.Message };
        }

        _testing = false;
        StateHasChanged();
    }
}
