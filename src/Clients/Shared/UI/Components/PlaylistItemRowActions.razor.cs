using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PlaylistItemRowActions
{
    [Parameter] public bool IsMusic { get; set; }
    [Parameter] public AudioQueueItem Track { get; set; } = default!;
    [Parameter] public MediaCardViewModel? Model { get; set; }
    [Parameter] public string? Href { get; set; }
    [Parameter] public EventCallback OnRemove { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; } = true;
    [Parameter] public bool ProgressEnabled { get; set; }
    [Parameter] public bool WatchedStatusEnabled { get; set; }
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool WatchStateMenuEnabled { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }
    [Parameter] public EventCallback OnWatchStateChanged { get; set; }
}
