using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;

namespace K7.Clients.MAUI.Services;

public sealed class PageFilterStorage : IPageFilterStorage
{
    private static string StorageKey(string key) => "pageFilters." + key;

    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = Preferences.Get(StorageKey(key), null);
        return Task.FromResult(PageFilterJson.Deserialize<T>(json));
    }

    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Set(StorageKey(key), PageFilterJson.Serialize(value));
        return Task.CompletedTask;
    }

    public Task ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(StorageKey(key));
        return Task.CompletedTask;
    }
}
