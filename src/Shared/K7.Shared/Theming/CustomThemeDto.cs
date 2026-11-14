namespace K7.Shared.Theming;

/// <summary>A theme made or imported by the user, stored with their appearance preferences.</summary>
public sealed record CustomThemeDto
{
    public const string IdPrefix = "custom-";
    public const int MaxNameLength = 60;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ThemePaletteDto Palette { get; set; } = new();

    public static string NewId() => $"{IdPrefix}{Guid.NewGuid():N}";
}
