using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Theming;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Copy of the appearance preferences of one account on the device. The preferences only come
/// from the server once the user is signed in; until then the app would show the default card
/// style and load the light home feed, then change both. With the copy, the app opens with the
/// right look and asks for the right feed straight away. The server stays the reference: its
/// answer replaces the copy.
/// </summary>
public static class AppearanceCache
{
    private sealed record Entry(string UserId, AppearancePreferencesDto Preferences, MediaCardPreferencesDto? ServerDefaultCards);

    /// <summary>What the device remembers: the preferences of the user, and the card style of the server they may follow.</summary>
    public sealed record CachedAppearance(AppearancePreferencesDto Preferences, MediaCardPreferencesDto? ServerDefaultCards);

    /// <summary>The stored preferences of this user, or null when there are none for them.</summary>
    public static CachedAppearance? TryLoad(IDeviceStorageService storage, string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        try
        {
            var json = storage.Get(PreferenceKeys.APPEARANCE_PREFERENCES_CACHE);
            if (string.IsNullOrEmpty(json))
                return null;

            var entry = JsonSerializer.Deserialize<Entry>(json);
            return entry?.Preferences is not null && string.Equals(entry.UserId, userId, StringComparison.Ordinal)
                ? new CachedAppearance(entry.Preferences, entry.ServerDefaultCards)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            // Unreadable or from another version: wait for the server.
            return null;
        }
    }

    public static void Save(
        IDeviceStorageService storage,
        string? userId,
        AppearancePreferencesDto preferences,
        MediaCardPreferencesDto? serverDefaultCards)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        try
        {
            storage.Set(PreferenceKeys.APPEARANCE_PREFERENCES_CACHE, JsonSerializer.Serialize(new Entry(userId, preferences, serverDefaultCards)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // Storage not available (private mode, quota): the next start waits for the server.
        }
    }
}
