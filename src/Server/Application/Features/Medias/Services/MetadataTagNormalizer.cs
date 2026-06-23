using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Features.Medias.Services;

public static partial class MetadataTagNormalizer
{
    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(c);
            else if (c is ' ' or '-' or '_' or '&' or '/')
                builder.Append('-');
        }

        return CollapseDashes(builder.ToString());
    }

    public static IEnumerable<string> SplitMultiValue(string value, bool splitGenreDelimiters)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        if (!splitGenreDelimiters)
        {
            yield return value.Trim();
            yield break;
        }

        foreach (var part in MultiValueSplitPattern().Split(value))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    private static string CollapseDashes(string value) =>
        CollapseDashPattern().Replace(value, "-").Trim('-');

    [GeneratedRegex(@"[\s/&]+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiValueSplitPattern();

    [GeneratedRegex(@"-+", RegexOptions.CultureInvariant)]
    private static partial Regex CollapseDashPattern();
}
