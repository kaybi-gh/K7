using K7.Shared.Dtos;

namespace K7.Clients.Shared.Pages.Music;

public partial class MusicStats
{
    private MusicStatsDto? _stats;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;

        try
        {
            _stats = await K7ServerService.GetMusicStatsAsync();
        }
        catch
        {
            _stats = null;
        }

        _loading = false;
    }
}
