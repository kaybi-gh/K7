using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminVideoPlayerSettingsPanel
{
    private VideoPlayerSettingsDto? _settings;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                        ?? new VideoPlayerSettingsDto();
        }
        catch
        {
            _settings = new VideoPlayerSettingsDto();
        }

        _loading = false;
    }

    private async Task SaveAsync()
    {
        if (_settings is null)
            return;

        await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_settings);
    }

    private async Task ResetAsync()
    {
        await ServerPreferencesService.DeleteServerVideoPlayerSettingsAsync();
        _settings = new VideoPlayerSettingsDto();
    }
}
