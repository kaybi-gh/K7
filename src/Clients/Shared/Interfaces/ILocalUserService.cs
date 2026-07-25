using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ILocalUserService
{
    List<LocalUser> GetAll();
    LocalUser? GetLastActive();
    void SaveOrUpdate(LocalUser user);
    void UpdateRefreshToken(string identityUserId, string refreshToken);
    void Remove(string identityUserId);
    void SetLastActiveId(string identityUserId);
    void ClearLastActiveId();
    void SetPin(string identityUserId, string? pin);
    bool VerifyPin(string identityUserId, string pin);
    bool IsSingleUserMode { get; set; }
    /// <summary>
    /// Marks a profile as unlocked for solo-device auto-login (after PIN or when none).
    /// </summary>
    void MarkSingleUserUnlocked(string identityUserId);
    void ClearSingleUserUnlocked();
    bool IsSingleUserUnlocked(string identityUserId);
}
