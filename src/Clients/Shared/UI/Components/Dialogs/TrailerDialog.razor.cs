using K7.Clients.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class TrailerDialog
{
    [Parameter] public string TrailerKey { get; set; } = string.Empty;
    [Parameter] public string TrailerSite { get; set; } = "YouTube";
    [Parameter] public string TrailerName { get; set; } = string.Empty;

    private string? _embedUrl => TrailerPlaybackHelper.TryBuildEmbedUrl(TrailerSite, TrailerKey);
}
