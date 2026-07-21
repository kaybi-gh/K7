using System.Reflection;

namespace K7.Shared;

public static class PreferenceKeyCatalog
{
    /// <summary>
    /// Connection and identity keys kept when clearing local customizations.
    /// </summary>
    public static readonly HashSet<string> PreservedIdentityKeyNames =
    [
        PreferenceKeys.K7_SERVER_URL.Name,
        PreferenceKeys.DEVICE_ID.Name,
        PreferenceKeys.ACCESS_TOKEN.Name,
        PreferenceKeys.REFRESH_TOKEN.Name,
        PreferenceKeys.DEVICE_ATTACHED_USER_ID.Name,
        PreferenceKeys.SERVER_INFO.Name,
        PreferenceKeys.LOCAL_USERS.Name,
        PreferenceKeys.SINGLE_USER_MODE.Name,
        PreferenceKeys.LAST_ACTIVE_USER_ID.Name,
        PreferenceKeys.ACTIVE_SHARED_PROFILE_ID.Name,
        PreferenceKeys.LAST_ACTIVE_SHARED_PROFILE_ID.Name,
        PreferenceKeys.LAST_PROFILE_SELECT_KIND.Name,
        PreferenceKeys.LAST_PROFILE_SELECT_ID.Name,
        PreferenceKeys.LAST_PROFILE_SELECT_AT.Name,
        PreferenceKeys.SHARED_PROFILES_CACHE.Name,
    ];

    public static readonly HashSet<string> PreservedOnPreferencesClear =
    [
        PreferenceKeys.K7_SERVER_URL.Name,
        PreferenceKeys.DEVICE_ID.Name,
    ];

    public static IReadOnlyList<string> AllKeyNames { get; } = typeof(PreferenceKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType.IsGenericType
            && field.FieldType.GetGenericTypeDefinition() == typeof(PreferenceKey<>))
        .Select(field => GetKeyName(field))
        .ToList();

    public static IEnumerable<string> CustomizationKeyNames =>
        AllKeyNames.Where(name => !PreservedIdentityKeyNames.Contains(name));

    public static IReadOnlyDictionary<string, string?> SnapshotPreservedStringValues(
        Func<string, string?> readValue)
    {
        var snapshot = new Dictionary<string, string?>(PreservedOnPreferencesClear.Count);
        foreach (var name in PreservedOnPreferencesClear)
            snapshot[name] = readValue(name);

        return snapshot;
    }

    public static void RestorePreservedStringValues(
        IReadOnlyDictionary<string, string?> snapshot,
        Action<string, string> writeValue)
    {
        foreach (var name in PreservedOnPreferencesClear)
        {
            if (!snapshot.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
                continue;

            writeValue(name, value);
        }
    }

    private static string GetKeyName(FieldInfo field)
    {
        var key = field.GetValue(null)
            ?? throw new InvalidOperationException($"Preference key field {field.Name} is null.");

        return (string)field.FieldType.GetProperty(nameof(PreferenceKey<object>.Name))!.GetValue(key)!;
    }
}
