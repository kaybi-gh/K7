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
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ThemeService ThemeService { get; set; } = default!;

    private string _defaultLanguage = "en";
    private string _savedDefaultLanguage = "en";
    private bool _saving;
    private AppearanceSettingsPanel? _lookPanel;
    private List<CustomThemeDto> _serverThemes = [];
    private bool _savingThemes;
    private bool _mediaLinkPreviewsEnabled = true;
    private bool _linkPreviewsSaving;
    private SettingsSaveState _linkPreviewsSaveState;
    private bool _isLoading = true;

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

    /// <summary>The look and the language wait for Save: they reach every account.</summary>
    private bool IsDirty => (_lookPanel?.IsDirty ?? false) || _defaultLanguage != _savedDefaultLanguage;

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
                _savedDefaultLanguage = serverInfo.DefaultLanguage;
                _serverThemes = serverInfo.ServerThemes;
            }

            var flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
            _mediaLinkPreviewsEnabled = flags.MediaLinkPreviewsEnabled;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnLookStateChanged() => StateHasChanged();

    private void OnDefaultLanguageChanged(string language) => _defaultLanguage = language;

    private async Task SaveAsync()
    {
        if (_saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            if (_defaultLanguage != _savedDefaultLanguage)
            {
                await ServerInfoService.UpdateDefaultLanguageAsync(_defaultLanguage);
                _savedDefaultLanguage = _defaultLanguage;
            }

            // The panel reports its own failures.
            if (_lookPanel is { IsDirty: true })
                await _lookPanel.SaveChangesAsync();

            if (!IsDirty)
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

    private void CancelChanges()
    {
        _defaultLanguage = _savedDefaultLanguage;
        _lookPanel?.CancelChanges();
    }

    /// <summary>Theme and corners back on the built-in look. Like any change, it waits for Save.</summary>
    private Task ResetLookAsync() => _lookPanel?.ResetAsync() ?? Task.CompletedTask;

    private Task AddServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes, theme]);

    private Task ReplaceServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes.Select(t => t.Id == theme.Id ? theme : t)]);

    private Task RemoveServerThemeAsync(CustomThemeDto theme) =>
        SaveServerThemesAsync([.. _serverThemes.Where(t => t.Id != theme.Id)]);

    /// <summary>
    /// Server themes are saved as soon as they change: a theme has to exist on the server
    /// before it can be picked as a default.
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

            // The server puts the default back on the dark base when its theme is deleted. The
            // look panel follows the themes of the service: an edited theme, a deleted one.
            var defaultThemeId = ThemeService.ServerDefaultThemeId;
            if (themes.All(t => t.Id != defaultThemeId) && !BuiltInThemes.Contains(defaultThemeId))
                defaultThemeId = BuiltInThemes.DefaultDarkId;

            ThemeService.SetServerDefault(defaultThemeId, _serverThemes);
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

    // Link previews and Appearance are the same page: render again when only the
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

}
