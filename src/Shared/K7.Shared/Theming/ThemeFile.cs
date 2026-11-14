using System.Text.Json;
using System.Text.Json.Serialization;

namespace K7.Shared.Theming;

public enum ThemeFileError
{
    None,
    Empty,
    TooLarge,
    InvalidJson,
    InvalidFormat,
    InvalidColors
}

/// <summary>
/// The shareable form of a custom theme: a small JSON document with a name and nine colors.
/// Nothing else is read from it, so a shared file cannot carry CSS or scripts.
/// </summary>
public static class ThemeFile
{
    public const string Format = "k7-theme";
    public const int Version = 1;
    public const int MaxLength = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(CustomThemeDto theme) =>
        JsonSerializer.Serialize(new Document(Format, Version, theme.Name, theme.Palette), JsonOptions);

    /// <summary>Reads a theme file. The returned theme has a fresh id.</summary>
    public static ThemeFileError TryParse(string? json, out CustomThemeDto? theme)
    {
        theme = null;

        if (string.IsNullOrWhiteSpace(json))
            return ThemeFileError.Empty;

        if (json.Length > MaxLength)
            return ThemeFileError.TooLarge;

        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return ThemeFileError.InvalidJson;
        }

        if (document is null
            || !string.Equals(document.Format, Format, StringComparison.Ordinal)
            || document.Version != Version
            || string.IsNullOrWhiteSpace(document.Name)
            || document.Palette is null)
        {
            return ThemeFileError.InvalidFormat;
        }

        if (!ThemePaletteValidator.HasValidColors(document.Palette))
            return ThemeFileError.InvalidColors;

        var name = document.Name.Trim();
        theme = new CustomThemeDto
        {
            Id = CustomThemeDto.NewId(),
            Name = name.Length > CustomThemeDto.MaxNameLength ? name[..CustomThemeDto.MaxNameLength] : name,
            Palette = document.Palette
        };
        return ThemeFileError.None;
    }

    private sealed record Document(string? Format, int Version, string? Name, ThemePaletteDto? Palette);
}
