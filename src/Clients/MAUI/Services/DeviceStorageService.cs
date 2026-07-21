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
            var secureValue = GetSecureString(key.Name);
            if (secureValue is not null)
                return (T?)(object)secureValue;

            var legacyValue = Preferences.Default.Get<string?>(key.Name, null);
            if (legacyValue is not null)
            {
                SetSecureString(key.Name, legacyValue);
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
            SetSecureString(key.Name, secureValue);
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
            RemoveSecureString(key.Name);

        Preferences.Default.Remove(key.Name);
    }

    public void ClearAllPreferences()
    {
        var preserved = PreferenceKeyCatalog.SnapshotPreservedStringValues(
            name => Preferences.Default.Get<string?>(name, null));

        foreach (var keyName in PreferenceKeyCatalog.CustomizationKeyNames)
            Preferences.Default.Remove(keyName);

        PreferenceKeyCatalog.RestorePreservedStringValues(
            preserved,
            (name, value) => Preferences.Default.Set(name, value));
    }

    // SecureStorage APIs are async. Calling GetAwaiter().GetResult() on the UI /
    // Blazor sync context deadlocks on Windows (PasswordVault completion posts back
    // to that same context). Run on the thread pool to avoid capturing it.
    private static string? GetSecureString(string key) =>
        Task.Run(async () => await SecureStorage.Default.GetAsync(key).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();

    private static void SetSecureString(string key, string value) =>
        Task.Run(async () =>
            {
                await SecureStorage.Default.SetAsync(key, value).ConfigureAwait(false);
            })
            .GetAwaiter()
            .GetResult();

    private static void RemoveSecureString(string key) =>
        Task.Run(() => SecureStorage.Default.Remove(key))
            .GetAwaiter()
            .GetResult();

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
