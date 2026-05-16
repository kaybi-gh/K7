using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsVideoPlayerPage
{
    private VideoPlayerSettingsDto? _settings;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
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

        await UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_settings);
    }

    private async Task ResetAsync()
    {
        await UserPreferencesService.ResetUserVideoPlayerSettingsAsync();
        _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
    }
}
