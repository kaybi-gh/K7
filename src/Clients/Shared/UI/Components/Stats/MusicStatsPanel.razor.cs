using K7.Shared.Dtos;

namespace K7.Clients.Shared.UI.Components.Stats;

public partial class MusicStatsPanel : IDisposable
{
    private MusicStatsDto? _stats;
    private bool _loading = true;
    private Timer? _debounceTimer;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.ProgressUpdated += OnProgressUpdated;
        await FetchStatsAsync();
    }

    private void OnProgressUpdated(Guid mediaId, double progress, bool isCompleted)
    {
        if (!isCompleted) return;

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await FetchStatsAsync();
                StateHasChanged();
            });
        }, null, 2000, Timeout.Infinite);
    }

    private async Task FetchStatsAsync()
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

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        _debounceTimer?.Dispose();
    }
}
