namespace K7.Shared;

public static class TrackSelectionLanguages
{
    public const string Original = "original";

    public static bool IsOriginal(string? language) =>
        string.Equals(language, Original, StringComparison.OrdinalIgnoreCase);
}
