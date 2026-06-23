namespace K7.Server.Application.Common.QueryExtensions;

public static class EfLikeQueryExtensions
{
    public static string ToContainsPattern(string value) => $"%{value.ToLowerInvariant()}%";
}
