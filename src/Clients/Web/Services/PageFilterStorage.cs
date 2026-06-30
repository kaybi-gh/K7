using Blazored.LocalStorage;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;

namespace K7.Clients.Web.Services;

public sealed class PageFilterStorage(ISyncLocalStorageService localStorage) : IPageFilterStorage
{
    private static string StorageKey(string key) => "pageFilters." + key;

    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = localStorage.GetItemAsString(StorageKey(key));
        return Task.FromResult(PageFilterJson.Deserialize<T>(json));
    }

    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        localStorage.SetItemAsString(StorageKey(key), PageFilterJson.Serialize(value));
        return Task.CompletedTask;
    }

    public Task ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        localStorage.RemoveItem(StorageKey(key));
        return Task.CompletedTask;
    }
}
