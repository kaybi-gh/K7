namespace K7.Shared.CustomNav;

public static class CustomNavLimits
{
    public const int MaxItems = 12;
    public const int MaxTitleLength = 80;
    public const int MaxRouteLength = 500;
    public const int MaxBrowseQueryLength = 2000;
    public const int MaxCardColorLength = 7;

    public static readonly string[] BrowseQueryKeys =
    [
        "mediaType",
        "sort",
        "view",
        "filter",
        "isearch",
        "genre",
        "studio",
        "network",
        "source"
    ];
}
