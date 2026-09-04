using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SerieSeasonTvHero
{
    private LiteSerieEpisodeDto? _episode;

    [Inject] private IStringLocalizer<SerieSeason> L { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public required string SerieId { get; set; }
    [Parameter] public string? SerieTitle { get; set; }
    [Parameter] public string? LogoUrl { get; set; }
    [Parameter] public LiteSerieEpisodeDto? Episode { get; set; }
    [Parameter] public bool CanResumePlayback { get; set; }
    [Parameter] public EventCallback<LiteSerieEpisodeDto> OnPlay { get; set; }
    [Parameter] public EventCallback<LiteSerieEpisodeDto> OnPlayFromBeginning { get; set; }
    [Parameter] public EventCallback OnOpenSynopsis { get; set; }

    private bool CanRestartEpisode =>
        CanResumePlayback && SeriePlaybackHelper.IsInProgress(_episode);

    private string PlayLabel
    {
        get
        {
            if (!CanRestartEpisode)
                return S["Play"];

            var position = PlaybackPositionFormatter.TryFormat(_episode?.UserState?.LastPlaybackPosition ?? 0);
            if (position is not null)
                return string.Format(S["ResumeAtTime"], position);

            return S["Resume"];
        }
    }

    protected override void OnParametersSet()
    {
        if (_episode is null || Episode?.Id == _episode.Id)
            _episode = Episode;
    }

    /// <summary>
    /// Update episode copy without a parent re-render (season carousel stays mounted).
    /// </summary>
    public void ApplyFocusedEpisode(LiteSerieEpisodeDto episode)
    {
        if (IsSameDisplay(episode))
            return;

        _episode = episode;
        StateHasChanged();
    }

    private bool IsSameDisplay(LiteSerieEpisodeDto episode)
    {
        if (_episode is null || _episode.Id != episode.Id)
            return false;

        return _episode.UserState?.ProgressPercentage == episode.UserState?.ProgressPercentage
            && _episode.UserState?.LastPlaybackPosition == episode.UserState?.LastPlaybackPosition
            && _episode.UserState?.IsCompleted == episode.UserState?.IsCompleted;
    }

    private Task OpenSynopsisAsync() => OnOpenSynopsis.InvokeAsync();

    private Task PlayAsync(LiteSerieEpisodeDto episode, bool fromBeginning) =>
        fromBeginning
            ? OnPlayFromBeginning.InvokeAsync(episode)
            : OnPlay.InvokeAsync(episode);
}
