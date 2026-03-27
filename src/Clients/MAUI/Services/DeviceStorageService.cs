using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.MAUI.Services;

public class DeviceStorageService : IDeviceStorageService
{
    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default)
    {
        if (IsPrimitive(typeof(T)))
        {
            return Preferences.Default.Get(key.Name, defaultValue);
        }

        var json = Preferences.Get(key.Name, null);
        return json is null ? defaultValue : JsonSerializer.Deserialize<T>(json);
    }

    public void Set<T>(PreferenceKey<T> key, T value)
    {
        if (IsPrimitive(typeof(T)))
        {
            Preferences.Default.Set(key.Name, value);
        }
        else
        {
            var json = JsonSerializer.Serialize(value);
            Preferences.Set(key.Name, json);
        }
    }

    public void Remove<T>(PreferenceKey<T> key)
    {
        Preferences.Default.Remove(key.Name);
    }

    private static bool IsPrimitive(Type type) =>
        type == typeof(bool) ||
        type == typeof(int) ||
        type == typeof(long) ||
        type == typeof(double) ||
        type == typeof(float) ||
        type == typeof(string) ||
        type == typeof(DateTime);
}
