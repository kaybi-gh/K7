using System.Globalization;
using System.Text;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Domain.Entities.Medias;

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

    public static string BuildSortTitlePattern(string query, bool supportsTrigramSearch)
    {
        var folded = RemoveDiacritics(query);
        if (string.IsNullOrWhiteSpace(folded))
            folded = query;

        return BuildTitlePattern(folded, supportsTrigramSearch);
    }

    public static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IQueryable<BaseMedia> WhereTitleOrSortTitleMatches(
        this IQueryable<BaseMedia> query,
        string titlePattern,
        string sortTitlePattern) =>
        query.Where(media =>
            (media.Title != null && EfLikeQueryExtensions.ILike(media.Title, titlePattern))
            || (media.SortTitle != null && EfLikeQueryExtensions.ILike(media.SortTitle, sortTitlePattern)));
}
