using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MediaSortTitleHelper
{
    public static string? Compute(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmed = title.Trim();

        var lApostropheMatch = LApostropheArticlePattern().Match(trimmed);
        if (lApostropheMatch.Success)
        {
            var lArticle = lApostropheMatch.Groups["article"].Value;
            var lRemainder = lApostropheMatch.Groups["remainder"].Value.Trim();
            if (lRemainder.Length > 0)
                return $"{lRemainder}, {lArticle}";
        }

        var match = LeadingArticlePattern().Match(trimmed);
        if (!match.Success)
            return trimmed;

        var article = match.Groups["article"].Value;
        var remainder = match.Groups["remainder"].Value.Trim();
        if (remainder.Length == 0)
            return trimmed;

        return $"{remainder}, {article}";
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
