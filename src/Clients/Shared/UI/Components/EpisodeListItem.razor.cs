using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class EpisodeListItem
{
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IStringLocalizer<EpisodeListItem> L { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieEpisodeDto Episode { get; set; }

    [Parameter]
    public string? StillUrl { get; set; }

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public EventCallback<LiteSerieEpisodeDto> OnPlay { get; set; }

    private Task PlayAsync() => OnPlay.HasDelegate
        ? OnPlay.InvokeAsync(Episode)
        : Task.CompletedTask;

    private Task NavigateToDetailAsync()
    {
        if (!string.IsNullOrEmpty(Href))
            NavigationManager.NavigateTo(Href);
        return Task.CompletedTask;
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }
}
