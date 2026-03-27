using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaCover
{
    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public required MediaPosterViewModel Model { get; set; }
    [Parameter] public bool FooterVisible { get; set; }
    [Parameter] public string Href { get; set; } = "#";
}
