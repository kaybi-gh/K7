using K7.Server.Application.Features.Medias.Services;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Picks a MusicBrainz name for the library metadata language.
/// Official names stay Japanese/Cyrillic/etc.; localized aliases (and Latin sort names)
/// are used when the library language is not that script.
/// </summary>
public static class MusicBrainzLocalizedName
{
    public static LocalizedName Resolve(
        string? officialName,
        string? sortName,
        IEnumerable<MusicBrainzNameAlias>? aliases,
        string? language,
        bool unfoldPersonSortName = false)
    {
        var official = NullIfEmpty(officialName);
        var usableAliases = (aliases ?? [])
            .Where(static a => !string.IsNullOrWhiteSpace(a.Name) && !a.IsSearchHint)
            .ToList();

        var requested = FindAlias(usableAliases, language);
        if (requested is not null)
            return FromAlias(requested, official);

        if (official is not null && IsScriptCompatible(official, language))
            return new LocalizedName(official, OriginalName: null, sortName);

        foreach (var fallback in MetadataSearchLanguageHelper.ResolveSearchLanguages(language, "en"))
        {
            if (SameLanguage(fallback, language))
                continue;

            var alias = FindAlias(usableAliases, fallback);
            if (alias is not null)
                return FromAlias(alias, official);
        }

        if (unfoldPersonSortName
            && official is not null
            && !IsMostlyLatin(official)
            && IsMostlyLatin(sortName))
        {
            var unfolded = MediaIdentityKeys.UnfoldCommaSortName(sortName);
            if (!string.IsNullOrWhiteSpace(unfolded))
                return new LocalizedName(unfolded, official, sortName);
        }

        return new LocalizedName(official ?? string.Empty, OriginalName: null, sortName);
    }

    private static LocalizedName FromAlias(MusicBrainzNameAlias alias, string? official)
    {
        var original = official is not null
            && !string.Equals(alias.Name, official, StringComparison.Ordinal)
            ? official
            : null;
        return new LocalizedName(alias.Name, original, alias.SortName);
    }

    private static MusicBrainzNameAlias? FindAlias(IReadOnlyList<MusicBrainzNameAlias> aliases, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var matches = aliases
            .Where(a => SameLanguage(a.Locale, language))
            .OrderByDescending(static a => a.IsPrimary)
            .ToList();
        return matches.Count == 0 ? null : matches[0];
    }

    private static bool SameLanguage(string? left, string? right)
    {
        var leftKey = LanguageKey(left);
        var rightKey = LanguageKey(right);
        return leftKey is not null
            && rightKey is not null
            && string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string? LanguageKey(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var trimmed = language.Trim();
        return trimmed.Length >= 2 ? trimmed[..2] : trimmed;
    }

    private static bool IsScriptCompatible(string name, string? language)
        => UsesLatinMetadata(language) == IsMostlyLatin(name);

    private static bool UsesLatinMetadata(string? language)
    {
        var key = LanguageKey(language);
        return key is not ("ja" or "zh" or "ko" or "ru" or "uk" or "bg" or "ar" or "he" or "el" or "th");
    }

    private static bool IsMostlyLatin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var letters = 0;
        var nonLatin = 0;
        foreach (var c in value)
        {
            if (!char.IsLetter(c))
                continue;

            letters++;
            // Beyond Latin Extended-B: CJK, Cyrillic, Arabic, etc.
            if (c > 0x024F)
                nonLatin++;
        }

        return letters > 0 && nonLatin * 2 < letters;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public readonly record struct LocalizedName(string Name, string? OriginalName, string? SortName);

public sealed record MusicBrainzNameAlias(string Name, string? Locale, bool IsPrimary, string? Type, string? SortName)
{
    public bool IsSearchHint =>
        Type is not null && Type.Contains("Search hint", StringComparison.OrdinalIgnoreCase);
}
