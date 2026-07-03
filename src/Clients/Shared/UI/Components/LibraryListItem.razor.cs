using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryListItem
{
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter, EditorRequired] public LiteMediaDto Item { get; set; } = default!;
    [Parameter] public string? Href { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }

    private LiteMediaThumbnailHelper.ThumbShape ThumbShape => LiteMediaThumbnailHelper.GetThumbShape(Item);
    private int ThumbWidth => LiteMediaThumbnailHelper.GetThumbSize(ThumbShape).Width;
    private int ThumbHeight => LiteMediaThumbnailHelper.GetThumbSize(ThumbShape).Height;
    private string RootClass =>
        $"library-list-item focusable {LiteMediaThumbnailHelper.GetShapeClass(ThumbShape)}";

    private string? ThumbnailUrl
    {
        get
        {
            var picture = LiteMediaThumbnailHelper.ResolvePicture(Item);
            return ApiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
        }
    }

    private string PlaceholderIcon => Item is LiteSerieEpisodeDto or LiteSerieSeasonDto or LiteSerieDto
        ? Phosphor.FilmSlate
        : Phosphor.Image;

    private string PrimaryTitle => Item switch
    {
        LiteSerieEpisodeDto episode =>
            $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2} \u2014 {Item.Title}",
        _ => Item.Title ?? S["Untitled"]
    };

    private string? SecondaryText => Item switch
    {
        LiteSerieEpisodeDto episode when !string.IsNullOrWhiteSpace(episode.SerieTitle) =>
            $"{episode.SerieTitle} \u00b7 {string.Format(S["SeasonNumber"], episode.SeasonNumber)}",
        LiteSerieSeasonDto season when !string.IsNullOrWhiteSpace(season.SerieTitle) =>
            season.SerieTitle,
        _ => GetDefaultSecondaryText()
    };

    private bool IsWatched => Item.UserState?.IsCompleted ?? false;
    private double Progress => Item.UserState?.ProgressPercentage ?? 0;

    private string? GetDefaultSecondaryText()
    {
        var parts = new List<string>();

        if (Item.ReleaseDate is not null)
            parts.Add(Item.ReleaseDate.Value.Year.ToString());

        switch (Item)
        {
            case LiteSerieEpisodeDto ep:
                if (ep.Duration is > 0)
                    parts.Add(FormatDuration(ep.Duration.Value));
                break;
            case LiteMusicTrackDto track:
                if (!string.IsNullOrEmpty(track.ArtistName))
                    parts.Add(track.ArtistName);
                if (track.Duration is > 0)
                    parts.Add(FormatDuration(track.Duration.Value));
                break;
        }

        return parts.Count > 0 ? string.Join(" - ", parts) : null;
    }

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}
