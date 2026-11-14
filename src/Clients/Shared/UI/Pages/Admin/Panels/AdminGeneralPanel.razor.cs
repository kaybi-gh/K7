using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminGeneralPanel : IDisposable
{
    private sealed record GeneralFormState(
        string Language,
        string ThemeCssDataAttribute,
        bool PlayThemeSongs);

    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ThemeService ThemeService { get; set; } = default!;

    private string _defaultLanguage = "en";
    private ThemeDefinition _defaultTheme = BuiltInThemes.DefaultDark;
    private string _savedDefaultThemeId = BuiltInThemes.DefaultDarkId;
    private List<CustomThemeDto> _serverThemes = [];
    private bool _savingThemes;
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _savedPlayThemeSongs = true;
    private bool _mediaLinkPreviewsEnabled = true;
    private bool _linkPreviewsSaving;
    private SettingsSaveState _linkPreviewsSaveState;
    private bool _isLoading = true;
    private bool _saving;
    private readonly SettingsFormTracker<GeneralFormState> _formTracker = new();

    /// <summary>Link previews are not part of Interface: own page, under its current or former address.</summary>
    private bool IsLinkPreviewsPage
    {
        get
        {
            var path = CustomNavPath.From(NavigationManager);
            return path.EndsWith("/link-previews", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/general", StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool IsThemeSongs => InterfaceSettingsSections.IsThemeSongs(NavigationManager);

    private string ActiveSection =>
        IsThemeSongs ? InterfaceSettingsSections.ThemeSongs : InterfaceSettingsSections.Appearance;

    /// <summary>What the default theme can be: a built-in theme or a theme of this server.</summary>
    private IReadOnlyList<ThemeDefinition> SelectableThemes =>
        [.. BuiltInThemes.All, .. _serverThemes.Select(ThemeService.ToDefinition)];

    private bool IsDirty =>
        _formTracker.IsDirty(new GeneralFormState(
            _defaultLanguage,
            _defaultTheme.Id,
            _playThemeSongs));

    public void Dispose() => NavigationManager.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        NavigationManager.LocationChanged += OnLocationChanged;

        try
        {
            var serverInfo = await ServerInfoService.GetServerInfoAsync();
            if (serverInfo is not null)
            {
                _defaultLanguage = serverInfo.DefaultLanguage;
                _serverThemes = serverInfo.ServerThemes;
                _defaultTheme = FindTheme(serverInfo.DefaultTheme);
                _savedDefaultThemeId = _defaultTheme.Id;
            }

            _videoPlayerSettings = await ServerPreferencesService.GetServerVideoPlayerSettingsAsync()
                                   ?? new VideoPlayerSettingsDto();
            _playThemeSongs = _videoPlayerSettings.PlayThemeSongs;

            var flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
            _mediaLinkPreviewsEnabled = flags.MediaLinkPreviewsEnabled;

            CaptureFormState();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void CaptureFormState()
    {
        _formTracker.Capture(new GeneralFormState(
            _defaultLanguage,
            _defaultTheme.Id,
            _playThemeSongs));
        _savedPlayThemeSongs = _playThemeSongs;
    }

    private void CancelChanges()
    {
        var state = _formTracker.Restore();
        _defaultLanguage = state.Language;
        _defaultTheme = FindTheme(state.ThemeCssDataAttribute);
        _playThemeSongs = state.PlayThemeSongs;
        _videoPlayerSettings.PlayThemeSongs = state.PlayThemeSongs;
    }

    private void OnDefaultLanguageChanged(string language)
    {
        _defaultLanguage = language;
        StateHasChanged();
    }

    private void OnDefaultThemeChanged(string themeId)
    {
        _defaultTheme = FindTheme(themeId);
        StateHasChanged();
    }

    private ThemeDefinition FindTheme(string? themeId) =>
        SelectableThemes.FirstOrDefault(t => t.Id == themeId) ?? BuiltInThemes.DefaultDark;

    private Task AddServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes, theme]);

    private Task ReplaceServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes.Select(t => t.Id == theme.Id ? theme : t)]);

    private Task RemoveServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes.Where(t => t.Id != theme.Id)]);

    /// <summary>
    /// Server themes are saved as soon as they change, apart from the Save bar: a theme has to
    /// exist on the server before it can be picked as the default.
    /// </summary>
    private async Task SaveServerThemesAsync(List<CustomThemeDto> themes)
    {
        if (_savingThemes)
            return;

        _savingThemes = true;
        try
        {
            await ServerInfoService.UpdateServerThemesAsync(themes);
            _serverThemes = themes;

            // The server puts the default back on the dark base when its theme is deleted.
            if (themes.All(t => t.Id != _savedDefaultThemeId) && !BuiltInThemes.Contains(_savedDefaultThemeId))
            {
                _savedDefaultThemeId = BuiltInThemes.DefaultDarkId;
                _formTracker.Capture(_formTracker.Restore() with { ThemeCssDataAttribute = _savedDefaultThemeId });
            }

            // Follows an edited theme, and leaves a deleted one.
            _defaultTheme = FindTheme(_defaultTheme.Id);
            ThemeService.SetServerDefault(_savedDefaultThemeId, _serverThemes);
            Snackbar.Add(L["ServerThemesSaved"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _savingThemes = false;
        }
    }

    private void OnPlayThemeSongsChanged(bool enabled)
    {
        _playThemeSongs = enabled;
        _videoPlayerSettings.PlayThemeSongs = enabled;
        StateHasChanged();
    }

    // Link previews, Appearance and Theme songs are the same page: render again when only the
    // address changes.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    /// <summary>A single switch: saved as soon as it is flipped, without the Save bar.</summary>
    private async Task OnMediaLinkPreviewsChangedAsync(bool enabled)
    {
        if (_linkPreviewsSaving || enabled == _mediaLinkPreviewsEnabled)
            return;

        _linkPreviewsSaving = true;
        _mediaLinkPreviewsEnabled = enabled;
        _linkPreviewsSaveState = SettingsSaveState.Saving;
        try
        {
            var current = await ServerPreferencesService.GetServerFeatureFlagsAsync();
            await ServerPreferencesService.UpdateServerFeatureFlagsAsync(
                current with { MediaLinkPreviewsEnabled = enabled });
            _linkPreviewsSaveState = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _mediaLinkPreviewsEnabled = !enabled;
            _linkPreviewsSaveState = SettingsSaveState.Failed;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _linkPreviewsSaving = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            await ServerInfoService.UpdateDefaultLanguageAsync(_defaultLanguage);
            await ServerInfoService.UpdateDefaultThemeAsync(_defaultTheme.Id);
            if (_playThemeSongs != _savedPlayThemeSongs)
                await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_videoPlayerSettings);

            _savedDefaultThemeId = _defaultTheme.Id;
            ThemeService.SetServerDefault(_savedDefaultThemeId, _serverThemes);
            CaptureFormState();
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }
}
