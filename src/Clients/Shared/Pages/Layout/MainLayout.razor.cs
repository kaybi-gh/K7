using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.Pages.Layout;

public partial class MainLayout
{
    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var isAuth = authState.User.Identity?.IsAuthenticated == true;
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (isAuth && !string.IsNullOrEmpty(userId))
            {
                var baseUri = NavigationManager.ToAbsoluteUri("/");
                await K7HubClient.EnsureStartedAsync(baseUri, userId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainLayout] Hub startup failed: {ex}");
        }
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= StateHasChanged;
        ThemeService.DarkModeEnabledOnChange -= StateHasChanged;
    }
}
