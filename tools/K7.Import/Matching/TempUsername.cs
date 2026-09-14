using System.Globalization;
using System.Text;

namespace K7.Import.Matching;

/// <summary>
/// Builds temp K7 usernames that satisfy ASP.NET Core Identity
/// <c>User.AllowedUserNameCharacters</c> (letters, digits, and <c>-._@+</c>).
/// </summary>
internal static class TempUsername
{
    // Default Identity AllowedUserNameCharacters (never customized in K7).
    private static readonly HashSet<char> Allowed = new(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+");

    public static string FromSource(string sourceType, string sourceName)
    {
        var slug = Sanitize(sourceName);
        if (slug.Length == 0)
            slug = "user";

        return $"{sourceType.ToLowerInvariant()}-{slug}";
    }

    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
                continue;

            var mapped = ch switch
            {
                ' ' or '\t' or '\r' or '\n' => '-',
                _ when Allowed.Contains(ch) => ch,
                _ when Allowed.Contains(char.ToLowerInvariant(ch)) => char.ToLowerInvariant(ch),
                _ => '-'
            };

            builder.Append(mapped);
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Trim('-', '.', '_', '@', '+');
    }
}
