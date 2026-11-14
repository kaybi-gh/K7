namespace K7.Shared.Theming;

/// <summary>Themes shipped with K7. Ids are stable: they are stored in user and server settings.</summary>
public static class BuiltInThemes
{
    public const string DefaultDarkId = "default-dark";
    public const string DefaultLightId = "default-light";

    public static readonly ThemeDefinition DefaultDark = new(DefaultDarkId, new ThemePaletteDto
    {
        IsDark = true,
        Background = "#0d0907",
        Surface = "#1a1410",
        SurfaceAlt = "#221a14",
        Text = "#f3ecde",
        TextSecondary = "#a99c87",
        TextMuted = "#8a7e6a",
        Accent = "#d9b060",
        OnAccent = "#241a0c",
        AccentText = "#d9b060"
    });

    public static readonly ThemeDefinition DefaultLight = new(DefaultLightId, new ThemePaletteDto
    {
        IsDark = false,
        Background = "#e4e3d9",
        Surface = "#fcfaf7",
        SurfaceAlt = "#eae9e1",
        Text = "#262420",
        TextSecondary = "#4a453c",
        TextMuted = "#5a554c",
        Accent = "#d9b060",
        OnAccent = "#241a0c",
        AccentText = "#6e4b16"
    });

    public static readonly ThemeDefinition Graphite = new("graphite", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#141414",
        Surface = "#1e1e1e",
        SurfaceAlt = "#272727",
        Text = "#ededed",
        TextSecondary = "#a8a8a8",
        TextMuted = "#878787",
        Accent = "#9db4c0",
        OnAccent = "#10181c",
        AccentText = "#9db4c0"
    });

    public static readonly ThemeDefinition Midnight = new("midnight", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#0a0e1a",
        Surface = "#121829",
        SurfaceAlt = "#1a2238",
        Text = "#e6eaf5",
        TextSecondary = "#9aa5c0",
        TextMuted = "#7a85a3",
        Accent = "#7aa2f7",
        OnAccent = "#0b1220",
        AccentText = "#7aa2f7"
    });

    public static readonly ThemeDefinition Oled = new("oled", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#000000",
        Surface = "#0f0f0f",
        SurfaceAlt = "#181818",
        Text = "#f2f2f2",
        TextSecondary = "#a6a6a6",
        TextMuted = "#858585",
        Accent = "#d9b060",
        OnAccent = "#241a0c",
        AccentText = "#d9b060"
    });

    public static readonly ThemeDefinition Plum = new("plum", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#140d16",
        Surface = "#1f1522",
        SurfaceAlt = "#2a1d2e",
        Text = "#f1e7f2",
        TextSecondary = "#b3a0b6",
        TextMuted = "#8f7c92",
        Accent = "#d48fd0",
        OnAccent = "#251026",
        AccentText = "#d48fd0"
    });

    public static readonly ThemeDefinition Forest = new("forest", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#0b110d",
        Surface = "#131c16",
        SurfaceAlt = "#1b271f",
        Text = "#e7efe6",
        TextSecondary = "#9db0a0",
        TextMuted = "#7c8f7f",
        Accent = "#8fc79a",
        OnAccent = "#0d2013",
        AccentText = "#8fc79a"
    });

    public static readonly ThemeDefinition Ember = new("ember", new ThemePaletteDto
    {
        IsDark = true,
        Background = "#120a09",
        Surface = "#1e1210",
        SurfaceAlt = "#291916",
        Text = "#f5e9e4",
        TextSecondary = "#b89f97",
        TextMuted = "#937b73",
        Accent = "#ef8a62",
        OnAccent = "#2a0f05",
        AccentText = "#ef8a62"
    });

    public static readonly ThemeDefinition Paper = new("paper", new ThemePaletteDto
    {
        IsDark = false,
        Background = "#f4f1ea",
        Surface = "#fffdf9",
        SurfaceAlt = "#ebe7dd",
        Text = "#2a2622",
        TextSecondary = "#544e45",
        TextMuted = "#6a6358",
        Accent = "#a85a14",
        OnAccent = "#fffdf9",
        AccentText = "#7a3f0b"
    });

    public static readonly ThemeDefinition Mist = new("mist", new ThemePaletteDto
    {
        IsDark = false,
        Background = "#e6e9ed",
        Surface = "#f8fafb",
        SurfaceAlt = "#dde2e7",
        Text = "#1f2429",
        TextSecondary = "#414a54",
        TextMuted = "#56606b",
        Accent = "#2f6fb0",
        OnAccent = "#f8fafb",
        AccentText = "#1f5288"
    });

    public static readonly IReadOnlyList<ThemeDefinition> All =
    [
        DefaultDark,
        DefaultLight,
        Graphite,
        Midnight,
        Oled,
        Plum,
        Forest,
        Ember,
        Paper,
        Mist
    ];

    public static ThemeDefinition? Find(string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    public static bool Contains(string? id) => Find(id) is not null;
}
