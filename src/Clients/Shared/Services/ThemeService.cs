using K7.Server.Domain.Enums;
using K7.Shared.Theming;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Holds the appearance of the signed-in user (theme, accent, text size, corners) resolved for
/// this device type. The layout listens to <see cref="ThemeOnChange"/> and writes
/// <see cref="Css"/> on the document.
/// </summary>
public class ThemeService
{
    private ThemeDefinition? _preview;

    public event Action? ThemeOnChange;

    public AppearancePreferencesDto Preferences { get; private set; } = new();

    public DeviceType DeviceType { get; private set; } = DeviceType.Unknown;

    public string ServerDefaultThemeId { get; private set; } = BuiltInThemes.DefaultDarkId;

    /// <summary>Themes made by an administrator, offered to every account.</summary>
    public IReadOnlyList<ThemeDefinition> ServerThemes { get; private set; } = [];

    /// <summary>
    /// False until the appearance to show is known. Until then the document keeps what the
    /// startup script restored from the previous visit.
    /// </summary>
    public bool IsResolved { get; private set; }

    public AppearanceSettingsDto Settings => Preferences.Resolve(DeviceType);

    public ThemeDefinition Theme => _preview ?? ResolveTheme(Settings.ThemeId);

    /// <summary>A previewed theme shows its own accent, not the accent override.</summary>
    public ThemeCss Css => ThemeCssBuilder.Build(
        Theme,
        _preview is null ? Settings : Settings with { AccentColor = null });

    public IReadOnlyList<ThemeDefinition> CustomThemes =>
        [.. Preferences.CustomThemes.Select(ToDefinition)];

    public ThemeDefinition ResolveTheme(string? themeId)
    {
        if (Preferences.FindCustomTheme(themeId) is { } custom)
            return ToDefinition(custom);

        return FindShared(themeId)
               ?? FindShared(ServerDefaultThemeId)
               ?? BuiltInThemes.DefaultDark;
    }

    /// <summary>
    /// Sets the look of the server: its own themes and the one shown to accounts that picked
    /// nothing. A default that is neither built-in nor a server theme falls back to the dark base.
    /// </summary>
    public void SetServerDefault(string? themeId, IReadOnlyList<CustomThemeDto>? serverThemes = null)
    {
        IReadOnlyList<ThemeDefinition> themes = [.. (serverThemes ?? []).Select(ToDefinition)];
        var resolved = BuiltInThemes.Contains(themeId) || themes.Any(t => t.Id == themeId)
            ? themeId!
            : BuiltInThemes.DefaultDarkId;

        if (resolved == ServerDefaultThemeId && themes.SequenceEqual(ServerThemes))
            return;

        ServerDefaultThemeId = resolved;
        ServerThemes = themes;
        if (IsResolved)
            ThemeOnChange?.Invoke();
    }

    /// <summary>Shows the server default look, for a visitor with nothing saved yet.</summary>
    public void ApplyServerDefault() => Apply(new AppearancePreferencesDto(), DeviceType);

    public void Apply(AppearancePreferencesDto preferences, DeviceType deviceType)
    {
        Preferences = preferences;
        DeviceType = deviceType;
        IsResolved = true;
        ThemeOnChange?.Invoke();
    }

    public void Update(AppearancePreferencesDto preferences) => Apply(preferences, DeviceType);

    /// <summary>Shows a theme being edited without saving it. Null goes back to the saved theme.</summary>
    public void SetPreview(ThemeDefinition? theme)
    {
        _preview = theme;
        IsResolved = true;
        ThemeOnChange?.Invoke();
    }

    private ThemeDefinition? FindShared(string? themeId) =>
        BuiltInThemes.Find(themeId) ?? ServerThemes.FirstOrDefault(t => t.Id == themeId);

    public static ThemeDefinition ToDefinition(CustomThemeDto theme) =>
        new(theme.Id, theme.Palette, IsBuiltIn: false, CustomName: theme.Name);
}
