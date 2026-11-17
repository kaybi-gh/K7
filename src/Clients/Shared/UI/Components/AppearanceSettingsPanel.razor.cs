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

    // Null stands for "all pages": the settings shared by every kind of media page.
    private MediaPageKind? _pageKind;
    private bool _loaded;

    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;

    /// <summary>
    /// Edits the defaults of the server (media pages and players, for the accounts that did not
    /// set them up) instead of the preferences of the user. One set for every device.
    /// </summary>
    [Parameter] public bool ServerDefaults { get; set; }

    /// <summary>The part of the settings on screen. Reset and the defaults state follow it.</summary>
    [Parameter] public AppearancePanelSection Section { get; set; }

    /// <summary>Raised when the save state or the defaults state changed, so the host can refresh its action bar.</summary>
    [Parameter] public EventCallback OnStateChanged { get; set; }

    public SettingsSaveState SaveState => _session?.State ?? SettingsSaveState.Idle;

    public bool IsBusy => _session?.IsBusy ?? false;

    public bool IsOnDefaults => Section switch
    {
        AppearancePanelSection.MediaPages => AllSettings.All(s => s.IsMediaPagesDefault),
        AppearancePanelSection.AudioPlayer => AllSettings.All(s => s.IsAudioPlayerDefault),
        AppearancePanelSection.VideoPlayer => AllSettings.All(s => s.IsVideoPlayerDefault),
        _ => _preferences.Default.IsLookDefault && _preferences.DeviceOverrides.Values.All(SameLookAsShared)
    };

    private IEnumerable<AppearanceSettingsDto> AllSettings =>
        _preferences.DeviceOverrides.Values.Append(_preferences.Default);

    private bool ScopeHasOverride => _preferences.HasDeviceOverride(_scope);

    // The sketch takes the shape of the device being set up, or of this one for "all devices".
    private DeviceType PreviewDevice => _scope is DeviceType.Unknown ? _deviceType : _scope;

    /// <summary>The set of the picked scope as stored: its own, or the shared one it follows.</summary>
    private AppearanceSettingsDto Stored => _preferences.Resolve(_scope);

    /// <summary>
    /// What the picked scope shows: its stored set, with the media pages and players of the
    /// server where the user set nothing up.
    /// </summary>
    private AppearanceSettingsDto Current => ServerDefaults ? Stored : Stored.WithServerSkins(ThemeService.ServerDefaultSkins);

    /// <summary>True when the look on screen is not the one this device keeps after leaving the page.</summary>
    private bool IsPreviewOnly => !ServerDefaults && !ReferenceEquals(Stored, _preferences.Resolve(_deviceType));

    /// <summary>What the picked kind of media page shows: its own settings, or the shared ones it follows.</summary>
    private MediaPageSkinDto CurrentPage => Current.PageSkin(_pageKind);

    private bool PageKindHasOverride => _pageKind is { } kind && Current.MediaPageOverrides.ContainsKey(kind);

    private string PageKindHint => _pageKind switch
    {
        null => L["MediaPageKindHint_All"],
        MediaPageKind.Season => L["MediaPageKindHint_Season"],
        _ => L[PageKindHasOverride ? "MediaPageKindHint_Own" : "MediaPageKindHint_Shared"]
    };

    // The sketch draws the kind of page picked, under a title of that kind.
    private string PagePreviewTitle => L[$"Preview_{_pageKind?.ToString() ?? nameof(MediaPageKind.Movie)}"];

    // A choice a device cannot show is not offered for it. "All devices" offers everything.
    private bool PhoneScope => !ServerDefaults && _scope is DeviceType.Phone;

    private bool TvScope => !ServerDefaults && _scope is DeviceType.TV;

    /// <summary>
    /// What the season page shows on the devices being set up. A TV is not one of them: its
    /// season page always is the carousel page.
    /// </summary>
    private SeasonPageLayout SeasonPageShown =>
        SkinClasses.SeasonLayout(Current, _scope is DeviceType.Unknown or DeviceType.TV ? DeviceType.Desktop : _scope);

    private IReadOnlyList<ButtonGroupOption<SeasonPageLayout>> SeasonPageOptions =>
    [
        .. SkinClasses.SeasonLayouts(DeviceType.Desktop).Select(layout => new ButtonGroupOption<SeasonPageLayout>(layout, L[$"SeasonPage_{layout}"]))
    ];

    // The pages that share the common settings, "all" first. A TV has no page for one episode:
    // picking an episode plays it.
    private IReadOnlyList<ButtonGroupOption<MediaPageKind?>> PageKindOptions =>
    [
        new(null, L["MediaPageKind_All"]),
        new(MediaPageKind.Movie, L["MediaPageKind_Movie"]),
        new(MediaPageKind.Series, L["MediaPageKind_Series"]),
        .. TvScope ? [] : new[] { new ButtonGroupOption<MediaPageKind?>(MediaPageKind.Episode, L["MediaPageKind_Episode"]) },
        new(MediaPageKind.Person, L["MediaPageKind_Person"])
    ];

    // The page of a season takes none of the common settings, it has its own: a group of its
    // own next to the others, so "all" does not seem to cover it. Nothing to choose on a TV,
    // where it always is the carousel page.
    private IReadOnlyList<ButtonGroupOption<MediaPageKind?>> SeasonKindOptions =>
        TvScope ? [] : [new(MediaPageKind.Season, L["MediaPageKind_Season"])];

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

    private IReadOnlyList<ButtonGroupOption<MiniPlayerPlacement>> MiniPlayerOptions =>
    [
        new(MiniPlayerPlacement.Auto, L["MiniPlayer_Bar"]),
        new(MiniPlayerPlacement.Bottom, L["MiniPlayer_Bottom"])
    ];

    private IReadOnlyList<ButtonGroupOption<MediaHeroStyle>> MediaHeroOptions =>
    [
        new(MediaHeroStyle.Fade, L["MediaPageHero_Fade"]),
        new(MediaHeroStyle.Cut, L["MediaPageHero_Cut"]),
        new(MediaHeroStyle.Banner, L["MediaPageHero_Banner"])
    ];

    private IReadOnlyList<ButtonGroupOption<MediaHeroSize>> MediaHeroSizeOptions =>
    [
        new(MediaHeroSize.Full, L["MediaPageHeroSize_Full"]),
        new(MediaHeroSize.Half, L["MediaPageHeroSize_Half"]),
        new(MediaHeroSize.Third, L["MediaPageHeroSize_Third"])
    ];

    private IReadOnlyList<ButtonGroupOption<MediaListStyle>> MediaListOptions =>
    [
        new(MediaListStyle.Carousel, L["MediaPageLists_Carousel"]),
        new(MediaListStyle.Grid, L["MediaPageLists_Grid"])
    ];

    private IReadOnlyList<ButtonGroupOption<AudioPlayerBackground>> AudioBackgroundOptions =>
    [
        new(AudioPlayerBackground.Blur, L["AudioBackground_Blur"]),
        new(AudioPlayerBackground.Gradient, L["AudioBackground_Gradient"]),
        new(AudioPlayerBackground.Plain, L["AudioBackground_Plain"])
    ];

    private IReadOnlyList<ButtonGroupOption<AudioPlayerLayout>> AudioLayoutOptions =>
    [
        new(AudioPlayerLayout.Side, L["AudioLayout_Side"]),
        new(AudioPlayerLayout.Centered, L["AudioLayout_Centered"])
    ];

    private IReadOnlyList<ButtonGroupOption<AudioArtworkShape>> AudioArtworkOptions =>
    [
        new(AudioArtworkShape.Square, L["AudioArtwork_Square"]),
        new(AudioArtworkShape.Round, L["AudioArtwork_Round"])
    ];

    private IReadOnlyList<ButtonGroupOption<AudioSeekStyle>> AudioSeekOptions =>
    [
        new(AudioSeekStyle.Waveform, L["AudioSeek_Waveform"]),
        new(AudioSeekStyle.Bar, L["AudioSeek_Bar"]),
        new(AudioSeekStyle.Ring, L["AudioSeek_Ring"]),
        new(AudioSeekStyle.PlainRing, L["AudioSeek_PlainRing"])
    ];

    private IReadOnlyList<ButtonGroupOption<PlayerAccent>> AudioAccentOptions =>
    [
        new(PlayerAccent.Auto, L["AudioAccent_Auto"]),
        new(PlayerAccent.Theme, L["Accent_Theme"])
    ];

    private IReadOnlyList<ButtonGroupOption<VideoControlsLayout>> VideoControlsOptions =>
    [
        new(VideoControlsLayout.Inline, L["VideoControls_Inline"]),
        new(VideoControlsLayout.Stacked, L["VideoControls_Stacked"])
    ];

    private IReadOnlyList<ButtonGroupOption<PlayerAccent>> VideoAccentOptions =>
    [
        new(PlayerAccent.Auto, L["VideoAccent_Auto"]),
        new(PlayerAccent.Theme, L["Accent_Theme"])
    ];

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();

        if (ServerDefaults)
        {
            AppearanceSettingsDto? skins = null;
            try
            {
                skins = (await ServerInfoService.GetServerInfoAsync())?.DefaultSkins;
            }
            catch (HttpRequestException)
            {
                // Offline: start from what the app knows, the save will report the failure.
                skins = ThemeService.ServerDefaultSkins;
            }

            _preferences = new AppearancePreferencesDto { Default = skins?.Copy() ?? new AppearanceSettingsDto() };
            _scope = DeviceType.Unknown;
        }
        else
        {
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
            Show(_scope);
        }

        _tracker.Capture(_preferences);
        _session = new SettingsAutoSaveSession<AppearancePreferencesDto>(
            _tracker,
            () => _loaded ? _preferences : null,
            SaveAsync,
            InvokeAsync,
            NotifyStateChanged,
            ex => Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error));
        _loaded = true;
        ThemeService.RemoteApplied += OnRemoteApplied;
        await OnStateChanged.InvokeAsync();
    }

    protected override void OnAfterRender(bool firstRender) => _session?.OnAfterRender();

    public void Dispose()
    {
        ThemeService.RemoteApplied -= OnRemoteApplied;
        _session?.Dispose();

        // Leave the preview of another device type.
        if (_loaded && !ServerDefaults && ThemeService.DeviceType != _deviceType)
            Show(_deviceType);
    }

    /// <summary>
    /// Back to the server look on every device. Custom themes are kept: they are the user's
    /// own work, not a setting.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_session is null)
            return;

        if (Section != AppearancePanelSection.Look)
        {
            // Only what this page shows goes back to the defaults, on every device type.
            await _session.RunAsync(async () =>
            {
                foreach (var settings in AllSettings)
                {
                    if (Section == AppearancePanelSection.MediaPages)
                        settings.ResetMediaPages();
                    else if (Section == AppearancePanelSection.AudioPlayer)
                        settings.ResetAudioPlayer();
                    else
                        settings.ResetVideoPlayer();
                }

                await SaveAsync();
                _tracker.Capture(_preferences);
                Show(_scope);
            });
            return;
        }

        await _session.RunAsync(async () =>
        {
            var reset = LookReset();
            if (reset.CustomThemes.Count == 0 && reset.Cards is null && reset.Default.IsDefault && reset.DeviceOverrides.Count == 0)
                await PreferencesService.ResetAppearancePreferencesAsync();
            else
                await PreferencesService.UpdateAppearancePreferencesAsync(reset);

            _preferences = reset;
            _tracker.Capture(reset);
            ThemeService.Apply(reset, _scope);
        });
    }

    /// <summary>
    /// The preferences once the look is reset: custom themes, cards and the skins of pages and
    /// players (the place of the mini player with them) are kept, they have their own pages.
    /// </summary>
    private AppearancePreferencesDto LookReset()
    {
        var reset = new AppearancePreferencesDto { CustomThemes = _preferences.CustomThemes, Cards = _preferences.Cards };
        CopySkins(_preferences.Default, reset.Default);
        foreach (var (deviceType, settings) in _preferences.DeviceOverrides)
        {
            if (settings.IsMediaPagesDefault && settings.IsPlayersDefault)
                continue;

            var kept = new AppearanceSettingsDto();
            CopySkins(settings, kept);
            reset.DeviceOverrides[deviceType] = kept;
        }

        return reset;
    }

    private static void CopySkins(AppearanceSettingsDto from, AppearanceSettingsDto to)
    {
        to.CopyMediaPagesFrom(from);
        to.CopyAudioPlayerFrom(from);
        to.CopyVideoPlayerFrom(from);
        to.MediaPagesOwn = from.MediaPagesOwn;
        to.AudioPlayerOwn = from.AudioPlayerOwn;
        to.VideoPlayerOwn = from.VideoPlayerOwn;
    }

    // A device type whose look is the default one only exists for its skins.
    private static bool SameLookAsShared(AppearanceSettingsDto settings) => settings.IsLookDefault;

    private void Change(Action<AppearanceSettingsDto> change)
    {
        var settings = Editable();
        MakeOwn(settings);
        change(settings);
        Commit();
    }

    private Task SaveAsync() => ServerDefaults
        ? ServerInfoService.UpdateDefaultSkinsAsync(_preferences.Default)
        : PreferencesService.UpdateAppearancePreferencesAsync(_preferences);

    /// <summary>
    /// Shows the preferences being edited. The defaults of the server are not a look of this
    /// account: they go to the app as what it falls back on.
    /// </summary>
    private void Show(DeviceType deviceType)
    {
        if (ServerDefaults)
            ThemeService.SetServerDefaultSkins(AppearanceSkins.Parse(AppearanceSkins.Serialize(_preferences.Default)));
        else
            ThemeService.Apply(_preferences, deviceType);
    }

    /// <summary>
    /// First change of the user to the part on screen (media pages or one player): it stops
    /// following the server, starting from what the server showed so nothing else moves.
    /// </summary>
    private void MakeOwn(AppearanceSettingsDto settings)
    {
        if (ServerDefaults)
            return;

        var server = ThemeService.ServerDefaultSkins;
        switch (Section)
        {
            case AppearancePanelSection.MediaPages when !settings.OwnsMediaPages:
                if (server is not null)
                    settings.CopyMediaPagesFrom(server);
                settings.MediaPagesOwn = true;
                break;
            case AppearancePanelSection.AudioPlayer when !settings.OwnsAudioPlayer:
                if (server is not null)
                    settings.CopyAudioPlayerFrom(server);
                settings.AudioPlayerOwn = true;
                break;
            case AppearancePanelSection.VideoPlayer when !settings.OwnsVideoPlayer:
                if (server is not null)
                    settings.CopyVideoPlayerFrom(server);
                settings.VideoPlayerOwn = true;
                break;
        }
    }

    // Another device changed the preferences while this page is open: it goes on from them
    // instead of saving its older copy over them at the next change.
    private void OnRemoteApplied() => InvokeAsync(() =>
    {
        if (ServerDefaults)
            return;

        _preferences = ThemeService.Preferences;
        _tracker.Capture(_preferences);
        if (_scope is not DeviceType.Unknown && ThemeService.DeviceType != _scope)
            Show(_scope);

        NotifyStateChanged();
    }).FireAndForget();

    private void NotifyStateChanged()
    {
        StateHasChanged();
        OnStateChanged.InvokeAsync().FireAndForget();
    }

    private void Commit()
    {
        Show(_scope);
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
            settings = _preferences.Default.Copy();
            _preferences.DeviceOverrides[_scope] = settings;
        }

        return settings;
    }

    /// <summary>
    /// A change to the media pages: to the shared settings, or to the kind of page picked,
    /// which gets its own copy of them on its first change.
    /// </summary>
    private void ChangePage(Action<MediaPageSkinDto> change)
    {
        var settings = Editable();
        MakeOwn(settings);
        if (_pageKind is not { } kind)
        {
            var shared = settings.SharedPageSkin();
            change(shared);
            settings.SetSharedPageSkin(shared);
        }
        else
        {
            if (!settings.MediaPageOverrides.TryGetValue(kind, out var own))
            {
                own = settings.SharedPageSkin();
                settings.MediaPageOverrides[kind] = own;
            }

            change(own);
        }

        Commit();
    }

    private void RemovePageKindOverride()
    {
        if (_pageKind is not { } kind)
            return;

        var settings = Editable();
        MakeOwn(settings);
        settings.MediaPageOverrides.Remove(kind);
        Commit();
    }

    private void OnScopeChanged(DeviceType scope)
    {
        _scope = scope;
        if (TvScope && _pageKind is MediaPageKind.Episode or MediaPageKind.Season)
            _pageKind = null;

        Show(_scope);
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
