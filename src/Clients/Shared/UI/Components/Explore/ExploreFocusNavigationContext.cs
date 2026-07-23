namespace K7.Clients.Shared.UI.Components.Explore;

/// <summary>
/// Cascaded from ExploreFeedHubView so carousel rows can stamp stable card ids and save focus.
/// </summary>
public sealed class ExploreFocusNavigationContext
{
    public required Guid GroupId { get; init; }

    public required Action<string> SaveMediaId { get; init; }

    public string GetCardElementId(string mediaId) => $"explore-card-{GroupId}-{mediaId}";
}
