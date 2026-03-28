using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class EpisodeListItem
{
    [Inject] private IMediaService K7ServerService { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieEpisodeDto Episode { get; set; }

    [Parameter]
    public string? StillUrl { get; set; }

    [Parameter]
    public bool IsExpanded { get; set; }

    [Parameter]
    public EventCallback OnToggleExpand { get; set; }

    [Parameter]
    public EventCallback OnPlay { get; set; }

    private SerieEpisodeDto? _fullEpisode;

    protected override async Task OnParametersSetAsync()
    {
        if (IsExpanded && _fullEpisode?.Id != Episode.Id)
        {
            var media = await K7ServerService.GetMediaAsync(Episode.Id);
            _fullEpisode = media as SerieEpisodeDto;
        }
    }

    private async Task HandlePlay()
    {
        await OnPlay.InvokeAsync();
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }
}
