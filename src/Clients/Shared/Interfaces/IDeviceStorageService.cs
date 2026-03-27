using K7.Shared;

namespace K7.Clients.Shared.Interfaces;

public interface IDeviceStorageService
{
    T? Get<T>(PreferenceKey<T> key, T? defaultValue = default);
    void Set<T>(PreferenceKey<T> key, T value);
    void Remove<T>(PreferenceKey<T> key);
}
