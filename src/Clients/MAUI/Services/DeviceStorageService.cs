using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.MAUI.Services;

public class DeviceStorageService : IDeviceStorageService
{
    private static readonly HashSet<string> SecureKeys =
    [
        PreferenceKeys.ACCESS_TOKEN.Name,
        PreferenceKeys.REFRESH_TOKEN.Name
    ];

    public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default)
    {
        if (IsSecureKey(key) && typeof(T) == typeof(string))
        {
            var secureValue = SecureStorage.Default.GetAsync(key.Name).GetAwaiter().GetResult();
            if (secureValue is not null)
                return (T?)(object)secureValue;

            var legacyValue = Preferences.Default.Get<string?>(key.Name, null);
            if (legacyValue is not null)
            {
                SecureStorage.Default.SetAsync(key.Name, legacyValue).GetAwaiter().GetResult();
                Preferences.Default.Remove(key.Name);
                return (T?)(object)legacyValue;
            }

            return defaultValue;
        }

        if (IsPrimitive(typeof(T)))
        {
            return Preferences.Default.Get(key.Name, defaultValue);
        }

        var json = Preferences.Get(key.Name, null);
        return json is null ? defaultValue : JsonSerializer.Deserialize<T>(json);
    }

    public void Set<T>(PreferenceKey<T> key, T value)
    {
        if (IsSecureKey(key) && value is string secureValue)
        {
            SecureStorage.Default.SetAsync(key.Name, secureValue).GetAwaiter().GetResult();
            Preferences.Default.Remove(key.Name);
            return;
        }

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
        if (IsSecureKey(key))
            SecureStorage.Default.Remove(key.Name);

        Preferences.Default.Remove(key.Name);
    }

    private static bool IsSecureKey<T>(PreferenceKey<T> key) => SecureKeys.Contains(key.Name);

    private static bool IsPrimitive(Type type) =>
        type == typeof(bool) ||
        type == typeof(int) ||
        type == typeof(long) ||
        type == typeof(double) ||
        type == typeof(float) ||
        type == typeof(string) ||
        type == typeof(DateTime);
}
