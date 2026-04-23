using K7.Shared.Dtos;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminActiveStreamsPanel : IDisposable
{
    private IReadOnlyList<ActiveStreamDto>? _streams;
    private bool _loading = true;
    private Timer? _refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        await FetchStreamsAsync();
        _refreshTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await FetchStreamsAsync();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async Task FetchStreamsAsync()
    {
        _loading = _streams is null;

        try
        {
            _streams = await K7ServerService.GetActiveStreamsAsync();
        }
        catch
        {
            _streams = null;
        }

        _loading = false;
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private static string? GetMediaRoute(ActiveStreamDto stream)
    {
        if (!stream.MediaId.HasValue)
            return null;

        var id = stream.MediaId.Value;

        return stream.MediaType switch
        {
            "Movie" => $"/movies/{id}",
            "Serie" => $"/series/{id}",
            "SerieEpisode" when stream.ParentId.HasValue => $"/series/{stream.ParentId.Value}",
            "MusicAlbum" => $"/music/albums/{id}",
            "MusicTrack" when stream.ParentId.HasValue => $"/music/albums/{stream.ParentId.Value}",
            _ => null
        };
    }
}
