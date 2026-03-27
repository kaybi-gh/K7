using System.Security.Cryptography;
using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;

namespace K7.Clients.Shared.Services;

public class LocalUserService(IDeviceStorageService storage) : ILocalUserService
{
    public List<LocalUser> GetAll()
    {
        var json = storage.Get(PreferenceKeys.LOCAL_USERS);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<LocalUser>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public LocalUser? GetLastActive()
    {
        var lastId = storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        if (string.IsNullOrEmpty(lastId))
            return null;

        return GetAll().FirstOrDefault(u => u.IdentityUserId == lastId);
    }

    public void SaveOrUpdate(LocalUser user)
    {
        var users = GetAll();
        var existing = users.FindIndex(u => u.IdentityUserId == user.IdentityUserId);
        if (existing >= 0)
        {
            user.PinHash = users[existing].PinHash;
            users[existing] = user;
        }
        else
        {
            users.Add(user);
        }

        Persist(users);
        SetLastActiveId(user.IdentityUserId);
    }

    public void Remove(string identityUserId)
    {
        var users = GetAll();
        users.RemoveAll(u => u.IdentityUserId == identityUserId);
        Persist(users);

        var lastId = storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        if (lastId == identityUserId)
            storage.Remove(PreferenceKeys.LAST_ACTIVE_USER_ID);
    }

    public void SetLastActiveId(string identityUserId) =>
        storage.Set(PreferenceKeys.LAST_ACTIVE_USER_ID, identityUserId);

    public void SetPin(string identityUserId, string? pin)
    {
        var users = GetAll();
        var user = users.FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user is null)
            return;

        user.PinHash = pin is null ? null : HashPin(pin);
        Persist(users);
    }

    public bool VerifyPin(string identityUserId, string pin)
    {
        var user = GetAll().FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user?.PinHash is null)
            return true;

        return VerifyPbkdf2(user.PinHash, pin);
    }

    public bool IsSingleUserMode
    {
        get => storage.Get(PreferenceKeys.SINGLE_USER_MODE);
        set => storage.Set(PreferenceKeys.SINGLE_USER_MODE, value);
    }

    private void Persist(List<LocalUser> users)
    {
        var json = JsonSerializer.Serialize(users);
        storage.Set(PreferenceKeys.LOCAL_USERS, json);
    }

    private static string HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 10_000, HashAlgorithmName.SHA256, 32);
        return $"$PBKDF2$iterations=10000${Convert.ToHexStringLower(salt)}${Convert.ToHexStringLower(hash)}";
    }

    private static bool VerifyPbkdf2(string storedHash, string pin)
    {
        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "PBKDF2")
            return false;

        var iterParts = parts[1].Split('=');
        if (iterParts.Length != 2 || !int.TryParse(iterParts[1], out var iterations))
            return false;

        var salt = Convert.FromHexString(parts[2]);
        var expectedHash = Convert.FromHexString(parts[3]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
