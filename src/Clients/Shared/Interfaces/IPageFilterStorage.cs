namespace K7.Clients.Shared.Interfaces;

public interface IPageFilterStorage
{
    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class;

    Task ClearAsync(string key, CancellationToken cancellationToken = default);
}
