using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
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

    private string _defaultLanguage = "en";
    private ThemeDefinition _defaultTheme = Themes.DefaultDark;
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

    private bool IsDirty =>
        _formTracker.IsDirty(new GeneralFormState(
            _defaultLanguage,
            _defaultTheme.CssDataAttribute,
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
                _defaultTheme = Themes.FromCssDataAttribute(serverInfo.DefaultTheme) ?? Themes.DefaultDark;
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
            _defaultTheme.CssDataAttribute,
            _playThemeSongs));
        _savedPlayThemeSongs = _playThemeSongs;
    }

    private void CancelChanges()
    {
        var state = _formTracker.Restore();
        _defaultLanguage = state.Language;
        _defaultTheme = Themes.FromCssDataAttribute(state.ThemeCssDataAttribute) ?? Themes.DefaultDark;
        _playThemeSongs = state.PlayThemeSongs;
        _videoPlayerSettings.PlayThemeSongs = state.PlayThemeSongs;
    }

    private void OnDefaultLanguageChanged(string language)
    {
        _defaultLanguage = language;
        StateHasChanged();
    }

    private void OnDefaultThemeChanged(ThemeDefinition theme)
    {
        _defaultTheme = theme;
        StateHasChanged();
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
            await ServerInfoService.UpdateDefaultThemeAsync(_defaultTheme.CssDataAttribute);
            if (_playThemeSongs != _savedPlayThemeSongs)
                await ServerPreferencesService.UpdateServerVideoPlayerSettingsAsync(_videoPlayerSettings);

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
