using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MediaSortTitleHelper
{
    public static string? Compute(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmed = Sanitize(title);
        if (trimmed.Length == 0)
            return null;

        string sortTitle;
        var lApostropheMatch = LApostropheArticlePattern().Match(trimmed);
        if (lApostropheMatch.Success)
        {
            var lArticle = lApostropheMatch.Groups["article"].Value;
            var lRemainder = lApostropheMatch.Groups["remainder"].Value.Trim();
            if (lRemainder.Length == 0)
                sortTitle = trimmed;
            else
                sortTitle = $"{lRemainder}, {lArticle}";
        }
        else
        {
            var match = LeadingArticlePattern().Match(trimmed);
            if (!match.Success)
            {
                sortTitle = trimmed;
            }
            else
            {
                var article = match.Groups["article"].Value;
                var remainder = match.Groups["remainder"].Value.Trim();
                sortTitle = remainder.Length == 0
                    ? trimmed
                    : $"{remainder}, {article}";
            }
        }

        sortTitle = RemoveDiacritics(sortTitle);
        return CapitalizeFirstLetter(sortTitle);
    }

    private static string Sanitize(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var ch in title.Normalize(NormalizationForm.FormC))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.Format
                or UnicodeCategory.Control
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse)
            {
                continue;
            }

            // Soft hyphen and other invisible separators that survive as punctuation.
            if (ch is '\u00AD' or '\u1806')
                continue;

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }

    private static string RemoveDiacritics(string value)
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

    private static string CapitalizeFirstLetter(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsLetter(value[i]))
                continue;

            if (char.IsUpper(value[i]))
                return value;

            return value[..i] + char.ToUpperInvariant(value[i]) + value[(i + 1)..];
        }

        return value;
    }

    [GeneratedRegex(
        @"^(?<article>L['\u2019])(?<remainder>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LApostropheArticlePattern();

    [GeneratedRegex(
        @"^(?:(?<article>The|An|A|Le|La|Les|Un|Une|Des)\s+)(?<remainder>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingArticlePattern();
}
