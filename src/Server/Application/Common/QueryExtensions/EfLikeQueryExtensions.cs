namespace K7.Server.Application.Common.QueryExtensions;

public static class EfLikeQueryExtensions
{
    public static string ToLowerSearchTerm(string value) => value.ToLowerInvariant();

    public static string ToContainsPattern(string value) => $"%{ToLowerSearchTerm(value)}%";

    public static string ToStartsWithPattern(string value) => $"{ToLowerSearchTerm(value)}%";

    public static string ToEndsWithPattern(string value) => $"%{ToLowerSearchTerm(value)}%";
}
