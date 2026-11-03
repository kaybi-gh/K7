using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.Shared.Services;

public sealed class NoOpDeviceStorageService : IDeviceStorageService
{
    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default) => defaultValue;

    public void Set<T>(PreferenceKey<T> key, T value)
    {
    }

    public void Remove<T>(PreferenceKey<T> key)
    {
    }

    public void ClearAllPreferences()
    {
    }
}
