using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Reads the last fetched <see cref="ServerInfoDto.GuestEnabled"/> from device storage.
/// Null when the device has not cached server-info yet.
/// </summary>
public static class CachedGuestAccess
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool? TryGetEnabled(IDeviceStorageService storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var json = storage.Get(PreferenceKeys.SERVER_INFO);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            var info = JsonSerializer.Deserialize<ServerInfoDto>(json, JsonOptions);
            return info?.GuestEnabled;
        }
        catch
        {
            return null;
        }
    }
}
