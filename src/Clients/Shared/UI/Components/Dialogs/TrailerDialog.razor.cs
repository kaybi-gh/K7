using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class TrailerDialog
{
    [Parameter] public string TrailerKey { get; set; } = string.Empty;
    [Parameter] public string TrailerSite { get; set; } = "YouTube";

    private string _embedUrl => TrailerSite switch
    {
        "YouTube" => $"https://www.youtube-nocookie.com/embed/{TrailerKey}?autoplay=1&enablejsapi=1",
        "Vimeo" => $"https://player.vimeo.com/video/{TrailerKey}?autoplay=1",
        _ => $"https://www.youtube-nocookie.com/embed/{TrailerKey}?autoplay=1&enablejsapi=1"
    };
}
