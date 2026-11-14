using System.Text.Json;

namespace K7.Shared.Theming;

/// <summary>
/// Themes made by an administrator and offered to every account. Same shape as a personal
/// theme (a name and nine colors), told apart by the prefix of their id.
/// </summary>
public static class ServerThemes
{
    public const string IdPrefix = "server-";
    public const int MaxCount = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string NewId() => $"{IdPrefix}{Guid.NewGuid():N}";

    public static bool IsValidId(string? id) => ThemePaletteValidator.IsValidThemeId(id, IdPrefix);

    /// <summary>True when the list can be stored: bounded, unique ids, named, well-formed colors.</summary>
    public static bool IsValid(IReadOnlyCollection<CustomThemeDto>? themes) =>
        themes is not null
        && themes.Count <= MaxCount
        && themes.All(t => ThemePaletteValidator.IsValidTheme(t, IdPrefix))
        && themes.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() == themes.Count;

    public static string Serialize(IReadOnlyCollection<CustomThemeDto> themes) =>
        JsonSerializer.Serialize(themes, JsonOptions);

    /// <summary>Reads the stored list. Anything unreadable or not valid is left out.</summary>
    public static List<CustomThemeDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var themes = JsonSerializer.Deserialize<List<CustomThemeDto?>>(json, JsonOptions) ?? [];
            return
            [
                .. themes
                    .Where(t => ThemePaletteValidator.IsValidTheme(t, IdPrefix))
                    .Select(t => t!)
                    .DistinctBy(t => t.Id, StringComparer.Ordinal)
                    .Take(MaxCount)
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Value of <c>data-theme</c> for a default theme id, which is a built-in theme or one of
    /// <paramref name="themes"/>. Unknown ids fall back to the dark base.
    /// </summary>
    public static string BaseAttribute(string? themeId, IReadOnlyCollection<CustomThemeDto> themes)
    {
        if (BuiltInThemes.Find(themeId) is { } builtIn)
            return builtIn.BaseAttribute;

        var theme = themes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.Ordinal));
        return theme is { Palette.IsDark: false } ? BuiltInThemes.DefaultLightId : BuiltInThemes.DefaultDarkId;
    }
}
