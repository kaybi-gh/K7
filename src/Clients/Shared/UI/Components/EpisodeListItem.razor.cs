using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class EpisodeListItem
{
    [Inject] private IStringLocalizer<SharedResource> SharedStrings { get; set; } = default!;
    [Inject] private IStringLocalizer<EpisodeListItem> L { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Parameter, EditorRequired]
    public required LiteSerieEpisodeDto Episode { get; set; }

    [Parameter]
    public string? StillUrl { get; set; }

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public EventCallback<LiteSerieEpisodeDto> OnPlay { get; set; }

    [Parameter]
    public EventCallback<LiteSerieEpisodeDto> OnWatchStateChanged { get; set; }

    private Task PlayAsync() => OnPlay.HasDelegate
        ? OnPlay.InvokeAsync(Episode)
        : Task.CompletedTask;

    private Task NavigateToDetailAsync()
    {
        if (!string.IsNullOrEmpty(Href))
            NavigationManager.NavigateTo(Href);
        return Task.CompletedTask;
    }

    private async Task ToggleWatchStateAsync()
    {
        var watched = Episode.UserState?.IsCompleted != true;
        var success = await WatchStateActions.ApplyAsync(
            MediaService,
            CacheStore,
            DialogService,
            Snackbar,
            SharedStrings,
            Episode.Id,
            watched,
            WatchStateScope.Item);

        if (!success)
            return;

        if (OnWatchStateChanged.HasDelegate)
            await OnWatchStateChanged.InvokeAsync(Episode);
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }
}
