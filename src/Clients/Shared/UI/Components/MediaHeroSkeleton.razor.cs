using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaHeroSkeleton
{
    [Parameter] public bool ShowCast { get; set; } = true;
    /// <summary>
    /// Movie/serie page hero: wide logo and 50vw column. Default matches TV season.
    /// </summary>
    [Parameter] public bool DetailLayout { get; set; }
    /// <summary>
    /// Person page hero: 2:3 portrait beside the bio, not a landscape logo.
    /// </summary>
    [Parameter] public bool PortraitLayout { get; set; }
    /// <summary>
    /// Desktop season page: compact logo, title row, and episode list rows.
    /// Default (no flag) matches the TV season hero.
    /// </summary>
    [Parameter] public bool SeasonLayout { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private string RootClass
    {
        get
        {
            var layout = PortraitLayout
                ? "media-hero-skeleton media-hero-skeleton--portrait"
                : SeasonLayout
                    ? "media-hero-skeleton media-hero-skeleton--season"
                    : DetailLayout
                        ? "media-hero-skeleton media-hero-skeleton--detail"
                        : "media-hero-skeleton";
            return string.IsNullOrEmpty(Class) ? layout : $"{layout} {Class}";
        }
    }
}
