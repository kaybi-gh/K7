namespace K7.Clients.Shared.Models;

public enum TvHubKind
{
    Home,
    LibraryGroup,
    ExploreGroup
}

public readonly record struct TvHubKey(TvHubKind Kind, Guid? GroupId = null)
{
    public static TvHubKey Home { get; } = new(TvHubKind.Home);

    public static TvHubKey ForLibraryGroup(Guid groupId) => new(TvHubKind.LibraryGroup, groupId);

    public static TvHubKey ForExploreGroup(Guid groupId) => new(TvHubKind.ExploreGroup, groupId);
}
