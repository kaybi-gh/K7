using Blazored.LocalStorage;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.Web.Services;

public class DeviceStorageService(ISyncLocalStorageService localStorageService) : IDeviceStorageService
{
    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default)
    {
        if (!localStorageService.ContainKey(key.Name))
            return defaultValue;

        return localStorageService.GetItem<T>(key.Name);
    }

    public void Set<T>(PreferenceKey<T> key, T value)
    {
        localStorageService.SetItem(key.Name, value);
    }

    public void Remove<T>(PreferenceKey<T> key)
    {
        localStorageService.RemoveItem(key.Name);
    }

    public void ClearAllPreferences()
    {
        var preserved = PreferenceKeyCatalog.SnapshotPreservedStringValues(ReadString);

        foreach (var keyName in PreferenceKeyCatalog.CustomizationKeyNames)
            localStorageService.RemoveItem(keyName);

        PreferenceKeyCatalog.RestorePreservedStringValues(
            preserved,
            (name, value) => localStorageService.SetItem(name, value));
    }

    private string? ReadString(string name) =>
        localStorageService.ContainKey(name) ? localStorageService.GetItem<string>(name) : null;
}
