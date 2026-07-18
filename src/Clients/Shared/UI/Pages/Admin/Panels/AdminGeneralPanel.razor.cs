using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services.Resources;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminGeneralPanel
{
    private sealed record GeneralFormState(string Language, string ThemeCssDataAttribute, bool PlayThemeSongs);

    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private string _defaultLanguage = "en";
    private ThemeDefinition _defaultTheme = Themes.DefaultDark;
    private VideoPlayerSettingsDto _videoPlayerSettings = new();
    private bool _playThemeSongs = true;
    private bool _savedPlayThemeSongs = true;
    private bool _isLoading = true;
    private bool _saving;
    private readonly SettingsFormTracker<GeneralFormState> _formTracker = new();

    private bool IsDirty =>
        _formTracker.IsDirty(new GeneralFormState(_defaultLanguage, _defaultTheme.CssDataAttribute, _playThemeSongs));

    protected override async Task OnInitializedAsync()
    {
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

            CaptureFormState();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void CaptureFormState()
    {
        _formTracker.Capture(new GeneralFormState(_defaultLanguage, _defaultTheme.CssDataAttribute, _playThemeSongs));
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
