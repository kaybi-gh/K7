namespace K7.Shared.Home;

public static class HomeLayoutRowTitles
{
    public const string ContinueWatching = "ContinueWatching";
    public const string RecommendedForYou = "RecommendedForYou";
    public const string NewlyAddedInPrefix = "NewlyAddedIn|";
    public const string BecauseYouWatchedPrefix = "BecauseYouWatched|";

    public static string NewlyAddedIn(string scope) => $"{NewlyAddedInPrefix}{scope}";

    public static bool TryParseNewlyAddedIn(string title, out string scope)
    {
        if (title.StartsWith(NewlyAddedInPrefix, StringComparison.Ordinal))
        {
            scope = title[NewlyAddedInPrefix.Length..];
            return true;
        }

        scope = string.Empty;
        return false;
    }

    public static string BecauseYouWatched(string seedTitle) => $"{BecauseYouWatchedPrefix}{seedTitle}";

    public static bool TryParseBecauseYouWatched(string title, out string seedTitle)
    {
        if (title.StartsWith(BecauseYouWatchedPrefix, StringComparison.Ordinal))
        {
            seedTitle = title[BecauseYouWatchedPrefix.Length..];
            return true;
        }

        seedTitle = string.Empty;
        return false;
    }
}
