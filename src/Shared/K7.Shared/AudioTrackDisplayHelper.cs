using System.Globalization;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared;

/// <summary>
/// Formats audio track names for probe storage, player menus, and HLS NAME attributes.
/// Player labels use the normalized language, then the original track name in
/// parentheses when it adds something (VFF, France, Canadien, commentary, ...).
/// </summary>
public static class AudioTrackDisplayHelper
{
    /// <summary>
    /// Chooses the stored track name from the container title and raw language tag.
    /// </summary>
    public static string? ResolveStoredName(string? title, string? rawLanguage)
    {
        var languageVariant = LanguageNormalizer.TryGetLanguageVariant(rawLanguage);
        var titleVariant = LanguageNormalizer.TryGetLanguageVariant(title);

        if (string.IsNullOrWhiteSpace(title) || LanguageNormalizer.IsGenericLanguageLabel(title))
            return languageVariant ?? titleVariant ?? (string.IsNullOrWhiteSpace(title) ? rawLanguage : title.Trim());

        if (LanguageNormalizer.IsLanguageVariantAlias(title) && titleVariant is not null)
            return titleVariant;

        if (languageVariant is not null && titleVariant is null)
            return $"{title.Trim()} ({languageVariant})";

        return title.Trim();
    }

    /// <summary>
    /// Player-menu label: normalized language, original name in parentheses, codec and channels.
    /// </summary>
    public static string FormatLabel(AudioFileTrackDto? track)
    {
        if (track is null)
            return "";

        var language = FormatLanguageName(track.Language ?? "und");
        var channels = track.ChannelLayout?.Split('(')[0].Trim();
        var codec = string.IsNullOrWhiteSpace(track.Codec) ? null : track.Codec;
        var details = !string.IsNullOrEmpty(channels)
            ? (codec is null ? channels : $"{codec} {channels}")
            : codec;

        var original = GetDistinctiveName(track.Name, track.Language, track.Codec);
        if (original is null)
            return details is null ? language : $"{language} ({details})";

        if (original.StartsWith(language, StringComparison.OrdinalIgnoreCase))
            return details is null ? original : $"{original} ({details})";

        return details is null
            ? $"{language} ({original})"
            : $"{language} ({original}) ({details})";
    }

    /// <summary>
    /// Subtitle-menu label: normalized language, original name in parentheses, type, codec.
    /// Pass a localized <paramref name="typeLabel"/> (Full / Forced / SDH).
    /// </summary>
    public static string FormatSubtitleLabel(SubtitleFileTrackDto? track, string typeLabel)
    {
        if (track is null)
            return "";

        var language = FormatLanguageName(track.Language ?? "und");
        var original = GetDistinctiveName(track.Name, track.Language, track.Codec);
        var head = original is null
            ? language
            : original.StartsWith(language, StringComparison.OrdinalIgnoreCase)
                ? original
                : $"{language} ({original})";

        return string.IsNullOrWhiteSpace(track.Codec)
            ? $"{head} - {typeLabel}"
            : $"{head} - {typeLabel} ({track.Codec})";
    }

    /// <summary>
    /// HLS EXT-X-MEDIA NAME. Unique within <paramref name="usedNames"/>.
    /// </summary>
    public static string FormatHlsName(string? name, string? language, int index, ISet<string> usedNames)
    {
        var distinctive = GetDistinctiveName(name, language);
        string candidate;
        if (distinctive is not null)
        {
            candidate = distinctive;
        }
        else if (LanguageNormalizer.IsRedundantTrackName(name, language))
        {
            candidate = string.IsNullOrWhiteSpace(language) ? $"Track {index}" : $"{language} {index}";
        }
        else
        {
            candidate = string.IsNullOrWhiteSpace(name) ? $"Track {index}" : name.Trim();
        }

        if (!usedNames.Add(candidate))
        {
            candidate = $"{candidate} ({index})";
            usedNames.Add(candidate);
        }

        return candidate;
    }

    public static string? GetDistinctiveName(string? name, string? language, string? codec = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();

        if (LanguageNormalizer.IsLanguageVariantAlias(trimmed)
            && LanguageNormalizer.TryGetLanguageVariant(trimmed) is { } variant)
        {
            return variant;
        }

        if (LanguageNormalizer.IsRedundantTrackName(trimmed, language))
            return null;

        if (!string.IsNullOrEmpty(codec) && trimmed.Contains(codec, StringComparison.OrdinalIgnoreCase))
            return null;

        return trimmed;
    }

    public static string FormatLanguageName(string code)
    {
        if (string.IsNullOrEmpty(code) || code == "und")
            return code;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            if (!string.IsNullOrEmpty(culture.DisplayName) && culture.DisplayName != code)
                return char.ToUpper(culture.DisplayName[0], CultureInfo.InvariantCulture) + culture.DisplayName[1..];
        }
        catch (CultureNotFoundException)
        {
        }

        return SupportedLanguages.GetDisplayLabel(code);
    }
}
