namespace K7.Shared.Home;

public static class HomeLayoutRowTitles
{
    public const string ContinueWatching = "ContinueWatching";
    public const string NewlyAddedInPrefix = "NewlyAddedIn|";

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
}
