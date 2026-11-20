using System.Text.Json;
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
    // How long a state shown here is expected back from the server as an echo of its own save.
    private static readonly TimeSpan EchoWindow = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly List<(string State, DateTimeOffset At)> _pendingEchoes = [];
    private string? _currentState;
    private ThemeDefinition? _preview;

    public ThemeService(TimeProvider? timeProvider = null) => _timeProvider = timeProvider ?? TimeProvider.System;

    public event Action? ThemeOnChange;

    /// <summary>
    /// Raised after preferences changed on another device were applied here: a page that edits
    /// them takes them as its new starting point.
    /// </summary>
    public event Action? RemoteApplied;

    public AppearancePreferencesDto Preferences { get; private set; } = new();

    public DeviceType DeviceType { get; private set; } = DeviceType.Unknown;

    /// <summary>
    /// Theme of the accounts that picked none, on every device. A type of device can have its
    /// own in <see cref="ServerSkins"/>.
    /// </summary>
    public string ServerDefaultThemeId { get; private set; } = BuiltInThemes.DefaultDarkId;

    /// <summary>Themes made by an administrator, offered to every account.</summary>
    public IReadOnlyList<ThemeDefinition> ServerThemes { get; private set; } = [];

    /// <summary>Card style an administrator set for accounts that chose none.</summary>
    public MediaCardPreferencesDto? ServerDefaultCards { get; private set; }

    /// <summary>The card style in effect: the user's own, else the server default, else the built-in card.</summary>
    public MediaCardPreferencesDto? EffectiveCards => Preferences.Cards ?? ServerDefaultCards;

    /// <summary>
    /// False until the appearance to show is known. Until then the document keeps what the
    /// startup script restored from the previous visit.
    /// </summary>
    public bool IsResolved { get; private set; }

    /// <summary>
    /// Media pages and players of the server, for the parts the user did not set up: the set
    /// shared by every device and the ones of the device types that have their own. Null means
    /// the built-in ones.
    /// </summary>
    public ServerSkinsDto? ServerSkins { get; private set; }

    /// <summary>The media pages and players of the server for the device type being shown.</summary>
    public AppearanceSettingsDto? ServerDefaultSkins => ServerSkins?.Resolve(DeviceType);

    /// <summary>
    /// What this device shows: the settings of the user for it, with the media pages and
    /// players of the server where the user set nothing up.
    /// </summary>
    public AppearanceSettingsDto Settings => Preferences.Resolve(DeviceType).WithServerSkins(ServerDefaultSkins);

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
               ?? FindShared(ServerSkins?.Resolve(DeviceType).ThemeId)
               ?? FindShared(ServerDefaultThemeId)
               ?? BuiltInThemes.DefaultDark;
    }

    /// <summary>Changes the theme shown to accounts that picked none, keeping the themes of the server.</summary>
    public void SetServerDefaultThemeId(string? themeId)
    {
        var resolved = FindShared(themeId)?.Id ?? BuiltInThemes.DefaultDarkId;
        if (resolved == ServerDefaultThemeId)
            return;

        ServerDefaultThemeId = resolved;
        if (IsResolved)
            ThemeOnChange?.Invoke();
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

    /// <summary>Sets the media pages and players of the server, for the parts the user did not set up.</summary>
    public void SetServerSkins(ServerSkinsDto? skins)
    {
        ServerSkins = skins;
        if (IsResolved)
            ThemeOnChange?.Invoke();
    }

    /// <summary>Sets one set of media pages and players of the server, shown on every device.</summary>
    public void SetServerDefaultSkins(AppearanceSettingsDto? skins) =>
        SetServerSkins(skins is null ? null : new ServerSkinsDto { Default = skins });

    /// <summary>Sets the card style of the server. Accounts with their own style do not change.</summary>
    public void SetServerDefaultCards(MediaCardPreferencesDto? cards)
    {
        ServerDefaultCards = cards;
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
        ExpectEcho(JsonSerializer.Serialize(preferences));
        ThemeOnChange?.Invoke();
    }

    /// <summary>
    /// Applies preferences sent by the server after a change on one of the devices of the user.
    /// The device that made the change gets them back too: a state it showed itself a moment
    /// ago is the echo of its own save, possibly older than what is on screen now, and is
    /// ignored. Only that: a state another device sent earlier is applied again when it comes
    /// back, or a setting switched on then off elsewhere would stay on here. Returns false when
    /// nothing was applied.
    /// </summary>
    public bool ApplyRemote(AppearancePreferencesDto preferences)
    {
        var state = JsonSerializer.Serialize(preferences);
        lock (_pendingEchoes)
        {
            DropExpiredEchoes();
            var echo = _pendingEchoes.FindIndex(e => e.State == state);
            if (echo >= 0)
            {
                _pendingEchoes.RemoveAt(echo);
                return false;
            }

            // Already what is on screen: nothing to change, and no page has to start over.
            if (state == _currentState)
                return false;

            _currentState = state;
        }

        Preferences = preferences;
        IsResolved = true;
        ThemeOnChange?.Invoke();
        RemoteApplied?.Invoke();
        return true;
    }

    public void Update(AppearancePreferencesDto preferences) => Apply(preferences, DeviceType);

    /// <summary>Shows a theme being edited without saving it. Null goes back to the saved theme.</summary>
    public void SetPreview(ThemeDefinition? theme)
    {
        _preview = theme;
        IsResolved = true;
        ThemeOnChange?.Invoke();
    }

    // A state shown from this device: its save will come back from the server.
    private void ExpectEcho(string state)
    {
        lock (_pendingEchoes)
        {
            DropExpiredEchoes();
            _currentState = state;
            if (!_pendingEchoes.Exists(e => e.State == state))
                _pendingEchoes.Add((state, _timeProvider.GetUtcNow()));
        }
    }

    private void DropExpiredEchoes()
    {
        var limit = _timeProvider.GetUtcNow() - EchoWindow;
        _pendingEchoes.RemoveAll(e => e.At < limit);
    }

    private ThemeDefinition? FindShared(string? themeId) =>
        BuiltInThemes.Find(themeId) ?? ServerThemes.FirstOrDefault(t => t.Id == themeId);

    public static ThemeDefinition ToDefinition(CustomThemeDto theme) =>
        new(theme.Id, theme.Palette, IsBuiltIn: false, CustomName: theme.Name);
}
