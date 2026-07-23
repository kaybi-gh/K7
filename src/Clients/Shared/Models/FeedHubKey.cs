namespace K7.Clients.Shared.Models;

public enum FeedHubKind
{
    Home,
    LibraryGroup,
    ExploreGroup
}

public readonly record struct FeedHubKey(FeedHubKind Kind, Guid? GroupId = null)
{
    public static FeedHubKey Home { get; } = new(FeedHubKind.Home);

    public static FeedHubKey ForLibraryGroup(Guid groupId) => new(FeedHubKind.LibraryGroup, groupId);

    public static FeedHubKey ForExploreGroup(Guid groupId) => new(FeedHubKind.ExploreGroup, groupId);
}
