using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Services;

public static class MediaTextSearchHelper
{
    private const int PrefixSearchMaxLength = 3;

    public static string BuildContainsPattern(string query)
    {
        var trimmed = query.Trim().ToLowerInvariant();
        return $"%{trimmed}%";
    }

    public static string BuildTitlePattern(string query, bool supportsTrigramSearch)
    {
        var trimmed = query.Trim().ToLowerInvariant();
        if (supportsTrigramSearch || trimmed.Length > PrefixSearchMaxLength)
            return $"%{trimmed}%";

        return $"{trimmed}%";
    }
}
