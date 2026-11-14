using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Personal appearance settings: theme, accent, text size and corners, for every device or for
/// one type of device picked in the device selector, and the user's own themes. Changes show at
/// once and save on their own. While a device type is picked the app previews its look.
/// </summary>
public partial class AppearanceSettingsPanel : IDisposable
{
    private readonly SettingsFormTracker<AppearancePreferencesDto> _tracker = new();
    private SettingsAutoSaveSession<AppearancePreferencesDto>? _session;
    private AppearancePreferencesDto _preferences = new();
    private DeviceType _deviceType = DeviceType.Unknown;

    // Unknown stands for "all devices": the shared settings.
    private DeviceType _scope = DeviceType.Unknown;
    private bool _loaded;

    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    /// <summary>Raised when the save state or the defaults state changed, so the host can refresh its action bar.</summary>
    [Parameter] public EventCallback OnStateChanged { get; set; }

    public SettingsSaveState SaveState => _session?.State ?? SettingsSaveState.Idle;

    public bool IsBusy => _session?.IsBusy ?? false;

    public bool IsOnDefaults => _preferences.Default.IsDefault && _preferences.DeviceOverrides.Count == 0;

    private bool ScopeHasOverride => _preferences.HasDeviceOverride(_scope);

    /// <summary>What the picked scope shows: its own set, or the shared one it follows.</summary>
    private AppearanceSettingsDto Current => _preferences.Resolve(_scope);

    /// <summary>True when the look on screen is not the one this device keeps after leaving the page.</summary>
    private bool IsPreviewOnly => !ReferenceEquals(Current, _preferences.Resolve(_deviceType));

    private string ScopeHint => _scope is DeviceType.Unknown
        ? L["ScopeHint_All"]
        : string.Format(L[ScopeHasOverride ? "ScopeHint_Own" : "ScopeHint_Shared"], L[$"Device_{_scope}"]);

    private IReadOnlyList<ButtonGroupOption<DeviceType>> ScopeOptions =>
    [
        new(DeviceType.Unknown, L["Scope_All"], Phosphor.Devices),
        new(DeviceType.Desktop, L["Scope_Desktop"], Phosphor.Desktop),
        new(DeviceType.Phone, L["Scope_Phone"], Phosphor.DeviceMobile),
        new(DeviceType.Tablet, L["Scope_Tablet"], Phosphor.DeviceTablet),
        new(DeviceType.TV, L["Scope_TV"], Phosphor.Television)
    ];

    private string ActiveThemeId => ThemeService.ResolveTheme(Current.ThemeId).Id;

    private string AccentValue => Current.AccentColor ?? ThemeService.ResolveTheme(Current.ThemeId).Palette.Accent;

    private bool ThemeLimitReached => _preferences.CustomThemes.Count >= AppearancePreferencesDto.MaxCustomThemes;

    private IReadOnlyList<ThemeDefinition> AllThemes =>
        [.. BuiltInThemes.All, .. ThemeService.ServerThemes, .. _preferences.CustomThemes.Select(ThemeService.ToDefinition)];

    private IReadOnlyList<ButtonGroupOption<AppearanceTextSize>> TextSizeOptions =>
    [
        new(AppearanceTextSize.Small, L["TextSize_Small"]),
        new(AppearanceTextSize.Default, L["TextSize_Default"]),
        new(AppearanceTextSize.Large, L["TextSize_Large"]),
        new(AppearanceTextSize.ExtraLarge, L["TextSize_ExtraLarge"])
    ];

    private IReadOnlyList<ButtonGroupOption<AppearanceCornerStyle>> CornerOptions =>
    [
        new(AppearanceCornerStyle.Square, L["Corners_Square"]),
        new(AppearanceCornerStyle.Default, L["Corners_Default"]),
        new(AppearanceCornerStyle.Round, L["Corners_Round"])
    ];

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();

        try
        {
            _preferences = await PreferencesService.GetAppearancePreferencesAsync();
        }
        catch (HttpRequestException)
        {
            // Offline: edit what the app already shows, the save will report the failure.
            _preferences = ThemeService.Preferences;
        }

        // Open on what this device shows: its own set when it has one, the shared one otherwise.
        _scope = _preferences.HasDeviceOverride(_deviceType) ? _deviceType : DeviceType.Unknown;
        ThemeService.Apply(_preferences, _scope);

        _tracker.Capture(_preferences);
        _session = new SettingsAutoSaveSession<AppearancePreferencesDto>(
            _tracker,
            () => _loaded ? _preferences : null,
            () => PreferencesService.UpdateAppearancePreferencesAsync(_preferences),
            InvokeAsync,
            NotifyStateChanged,
            ex => Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error));
        _loaded = true;
        await OnStateChanged.InvokeAsync();
    }

    protected override void OnAfterRender(bool firstRender) => _session?.OnAfterRender();

    public void Dispose()
    {
        _session?.Dispose();

        // Leave the preview of another device type.
        if (_loaded && ThemeService.DeviceType != _deviceType)
            ThemeService.Apply(_preferences, _deviceType);
    }

    /// <summary>
    /// Back to the server look on every device. Custom themes are kept: they are the user's
    /// own work, not a setting.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_session is null)
            return;

        await _session.RunAsync(async () =>
        {
            var reset = new AppearancePreferencesDto { CustomThemes = _preferences.CustomThemes };
            if (reset.CustomThemes.Count == 0)
                await PreferencesService.ResetAppearancePreferencesAsync();
            else
                await PreferencesService.UpdateAppearancePreferencesAsync(reset);

            _preferences = reset;
            _tracker.Capture(reset);
            ThemeService.Apply(reset, _scope);
        });
    }

    private void NotifyStateChanged()
    {
        StateHasChanged();
        OnStateChanged.InvokeAsync().FireAndForget();
    }

    private void Commit()
    {
        ThemeService.Apply(_preferences, _scope);
        OnStateChanged.InvokeAsync().FireAndForget();
    }

    /// <summary>
    /// The settings a change goes to. A device type that still follows the shared settings gets
    /// its own copy on its first change.
    /// </summary>
    private AppearanceSettingsDto Editable()
    {
        if (_scope is DeviceType.Unknown)
            return _preferences.Default;

        if (!_preferences.DeviceOverrides.TryGetValue(_scope, out var settings))
        {
            settings = _preferences.Default with { };
            _preferences.DeviceOverrides[_scope] = settings;
        }

        return settings;
    }

    private void OnScopeChanged(DeviceType scope)
    {
        _scope = scope;
        ThemeService.Apply(_preferences, _scope);
    }

    private void RemoveScopeOverride()
    {
        _preferences.DeviceOverrides.Remove(_scope);
        Commit();
    }

    private void OnThemeChanged(string themeId)
    {
        Editable().ThemeId = themeId;
        Commit();
    }

    private void OnAccentChanged(string color)
    {
        if (!ThemeColorMath.IsHex(color))
            return;

        Editable().AccentColor = ThemeColorMath.Normalize(color);
        Commit();
    }

    private void ClearAccent()
    {
        Editable().AccentColor = null;
        Commit();
    }

    private void OnTextSizeChanged(AppearanceTextSize size)
    {
        Editable().TextSize = size;
        Commit();
    }

    private void OnCornerStyleChanged(AppearanceCornerStyle style)
    {
        Editable().CornerStyle = style;
        Commit();
    }

    private void AddTheme(CustomThemeDto theme)
    {
        if (ThemeLimitReached)
            return;

        _preferences.CustomThemes.Add(theme);
        Editable().ThemeId = theme.Id;
        Commit();
    }

    private void ReplaceTheme(CustomThemeDto theme)
    {
        var index = _preferences.CustomThemes.FindIndex(t => t.Id == theme.Id);
        if (index < 0)
            return;

        _preferences.CustomThemes[index] = theme;
        Commit();
    }

    private void RemoveTheme(CustomThemeDto theme)
    {
        _preferences.CustomThemes.RemoveAll(t => t.Id == theme.Id);
        foreach (var settings in _preferences.DeviceOverrides.Values.Append(_preferences.Default))
        {
            if (settings.ThemeId == theme.Id)
                settings.ThemeId = null;
        }

        Commit();
    }
}
