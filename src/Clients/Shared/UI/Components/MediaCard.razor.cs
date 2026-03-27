using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public enum MediaCardVariant { Poster, Cover }

public partial class MediaCard
{
    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter] public MediaCardVariant Variant { get; set; } = MediaCardVariant.Poster;
    [Parameter] public string Href { get; set; } = "#";
    [Parameter] public bool OverlayEnabled { get; set; }
    [Parameter] public bool ProgressEnabled { get; set; }
    [Parameter] public bool WatchedStatusEnabled { get; set; }
    [Parameter] public bool FooterVisible { get; set; }
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }

    private string VariantClass => Variant == MediaCardVariant.Cover ? "media-card--cover" : "media-card--poster";

    private bool ProgressBarIsHidden() => Model.Progress == 0 || Model.Progress >= 100;
}
