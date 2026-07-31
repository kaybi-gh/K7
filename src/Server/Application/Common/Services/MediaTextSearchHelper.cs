using K7.Server.Application.Common.QueryExtensions;

namespace K7.Server.Application.Common.Services;

public static class MediaTextSearchHelper
{
    private const int PrefixSearchMaxLength = 3;

    public static string BuildContainsPattern(string query)
        => EfLikeQueryExtensions.ToContainsPattern(query);

    public static string BuildTitlePattern(string query, bool supportsTrigramSearch)
    {
        var trimmed = EfLikeQueryExtensions.Normalize(query);
        if (supportsTrigramSearch || trimmed.Length > PrefixSearchMaxLength)
            return $"%{trimmed}%";

        return $"{trimmed}%";
    }
}
