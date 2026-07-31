namespace K7.Server.Application.Common.QueryExtensions;

public static class EfLikeQueryExtensions
{
    /// <summary>
    /// Case-insensitive LIKE for EF queries. Translates to ILIKE on Postgres and
    /// LIKE lower(match) on Sqlite. Do not call outside of expression trees.
    /// </summary>
    public static bool ILike(string matchExpression, string pattern)
        => throw new InvalidOperationException($"{nameof(ILike)} can only be used in EF Core queries.");

    public static string ToContainsPattern(string value) => $"%{Normalize(value)}%";

    public static string ToStartsWithPattern(string value) => $"{Normalize(value)}%";

    public static string ToEndsWithPattern(string value) => $"%{Normalize(value)}";

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
