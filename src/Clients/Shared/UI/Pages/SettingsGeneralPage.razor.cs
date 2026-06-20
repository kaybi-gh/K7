using System.Globalization;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsGeneralPage : IDisposable
{
    private string? _backendUrl;
    private string _currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _backendUrl = ApiClient.HttpClient.BaseAddress?.AbsoluteUri;
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= StateHasChanged;
    }

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            L["WarningTitle"],
            L["ChangeServerUrlWarning"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (result == true)
        {
            ServerConnectionService.DisconnectAndReset();
        }
    }

    private async Task OnCultureChanged(string culture)
    {
        try
        {
            await UserService.UpdateUserLanguageAsync(culture);
        }
        catch
        {
            // Best effort
        }

        await JSRuntime.InvokeVoidAsync("blazorCulture.set", culture);
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }
}
