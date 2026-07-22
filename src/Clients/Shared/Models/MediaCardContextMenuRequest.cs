using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Models;

public enum MediaCardContextMenuAnchorKind
{
    Card,
    Activator
}

public sealed class MediaCardContextMenuRequest
{
    public required Guid OwnerId { get; init; }
    public required MediaCardViewModel Model { get; init; }
    public required ElementReference Anchor { get; init; }
    public MediaCardContextMenuAnchorKind AnchorKind { get; init; } = MediaCardContextMenuAnchorKind.Card;
    public string? Href { get; init; }
    public string? Title { get; init; }
    public bool ShowPlay { get; init; }
    public bool ShowRating { get; init; }
    public bool ShowReview { get; init; }
    public bool ShowPlaylist { get; init; }
    public bool ShowCollection { get; init; }
    public bool ShowWatchState { get; init; }
    public bool ExcludeMenuEnabled { get; init; }
    public bool ContinueWatchingMenuEnabled { get; init; }
    public bool IsAdmin { get; init; }
    public int? BulkEpisodeCount { get; init; }
    public EventCallback OnExcludeForSelf { get; init; }
    public EventCallback OnExcludeForOthers { get; init; }
    public EventCallback OnDismissFromContinueWatching { get; init; }
    public EventCallback OnWatchStateChanged { get; init; }
}
