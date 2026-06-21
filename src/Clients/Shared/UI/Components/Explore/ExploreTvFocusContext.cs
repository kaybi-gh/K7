using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.UI.Components.Explore;

public sealed class ExploreTvFocusContext
{
    public required Action<MediaCardViewModel> OnItemFocused { get; init; }
    public required Action<MediaCardViewModel> TrySetInitialItem { get; init; }
    public bool UseDetailedFeed { get; init; }
}
