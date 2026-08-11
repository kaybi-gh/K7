using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Helpers;

public static class NavigationHistoryHelper
{
    /// <summary>
    /// Leaves the current page via browser history (previous library / browse view).
    /// Falls back to home when history cannot be used.
    /// </summary>
    public static async Task NavigateBackOrHomeAsync(
        IJSRuntime jsRuntime,
        NavigationManager navigationManager,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("history.back", cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Fall through to home.
        }

        navigationManager.NavigateTo("/");
    }
}
