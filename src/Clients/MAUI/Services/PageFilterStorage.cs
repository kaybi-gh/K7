using System.Security.Claims;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.MAUI.Services;

public sealed class PageFilterStorage(AuthenticationStateProvider authenticationStateProvider) : IPageFilterStorage
{
    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = Preferences.Get(await StorageKeyAsync(key), null);
        return PageFilterJson.Deserialize<T>(json);
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Set(await StorageKeyAsync(key), PageFilterJson.Serialize(value));
    }

    public async Task ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(await StorageKeyAsync(key));
    }

    private async Task<string> StorageKeyAsync(string key)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? authState.User.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(userId)
            ? "pageFilters." + key
            : "pageFilters." + userId + "." + key;
    }
}
