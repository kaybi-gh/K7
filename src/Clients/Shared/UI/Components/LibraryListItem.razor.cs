using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryListItem
{
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public LiteMediaDto Item { get; set; } = default!;
    [Parameter] public string? Href { get; set; }

    private string? ThumbnailUrl => GetThumbnailUrl();
    private string? SecondaryText => GetSecondaryText();
    private bool IsWatched => Item.UserState?.IsCompleted ?? false;
    private double Progress => Item.UserState?.ProgressPercentage ?? 0;

    private string? GetThumbnailUrl()
    {
        var picture = Item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? Item.Pictures?.FirstOrDefault();

        return ApiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private string? GetSecondaryText()
    {
        var parts = new List<string>();

        if (Item.ReleaseDate is not null)
        {
            parts.Add(Item.ReleaseDate.Value.Year.ToString());
        }

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
