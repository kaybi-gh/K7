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
            var users = JsonSerializer.Deserialize<List<LocalUser>>(json) ?? [];
            foreach (var user in users)
            {
                if (user.PinHash is not null)
                    user.HasPin = true;
            }

            return users;
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
            var previous = users[existing];
            user.PinHash = previous.PinHash;
            if (user.PinHash is not null)
                user.HasPin = true;
            user.DisplayName ??= previous.DisplayName;
            user.UserId ??= previous.UserId;
            users[existing] = user;
        }
        else
        {
            users.Add(user);
        }

        Persist(users);
        SetLastActiveId(user.IdentityUserId);
    }

    public void UpdateRefreshToken(string identityUserId, string refreshToken)
    {
        if (string.IsNullOrEmpty(identityUserId) || string.IsNullOrEmpty(refreshToken))
            return;

        var users = GetAll();
        var user = users.FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user is null)
            return;

        user.RefreshToken = refreshToken;
        Persist(users);
    }

    public void ClearRefreshToken(string identityUserId)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return;

        var users = GetAll();
        var user = users.FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user is null)
            return;

        user.RefreshToken = string.Empty;
        Persist(users);
    }

    public void Remove(string identityUserId)
    {
        var users = GetAll();
        users.RemoveAll(u => u.IdentityUserId == identityUserId);
        Persist(users);

        var lastId = storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        if (lastId == identityUserId)
            storage.Remove(PreferenceKeys.LAST_ACTIVE_USER_ID);

        if (IsSingleUserUnlocked(identityUserId))
            ClearSingleUserUnlocked();
    }

    public void SetLastActiveId(string identityUserId) =>
        storage.Set(PreferenceKeys.LAST_ACTIVE_USER_ID, identityUserId);

    public void ClearLastActiveId() =>
        storage.Remove(PreferenceKeys.LAST_ACTIVE_USER_ID);

    public void SetPin(string identityUserId, string? pin)
    {
        var users = GetAll();
        var user = users.FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user is null)
            return;

        user.PinHash = pin is null ? null : PinVerifier.Hash(pin);
        user.HasPin = pin is not null;
        Persist(users);
    }

    public bool VerifyPin(string identityUserId, string pin)
    {
        var user = GetAll().FirstOrDefault(u => u.IdentityUserId == identityUserId);
        if (user?.PinHash is null)
            return true;

        return PinVerifier.Verify(user.PinHash, pin);
    }

    public bool IsSingleUserMode
    {
        get => storage.Get(PreferenceKeys.SINGLE_USER_MODE);
        set => storage.Set(PreferenceKeys.SINGLE_USER_MODE, value);
    }

    public void MarkSingleUserUnlocked(string identityUserId)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return;

        storage.Set(PreferenceKeys.SINGLE_USER_UNLOCKED_USER_ID, identityUserId);
    }

    public void ClearSingleUserUnlocked() =>
        storage.Remove(PreferenceKeys.SINGLE_USER_UNLOCKED_USER_ID);

    public bool IsSingleUserUnlocked(string identityUserId)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return false;

        var unlockedId = storage.Get(PreferenceKeys.SINGLE_USER_UNLOCKED_USER_ID);
        return string.Equals(unlockedId, identityUserId, StringComparison.Ordinal);
    }

    private void Persist(List<LocalUser> users)
    {
        var json = JsonSerializer.Serialize(users);
        storage.Set(PreferenceKeys.LOCAL_USERS, json);
    }

}
