using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface ILocalUserService
{
    List<LocalUser> GetAll();
    LocalUser? GetLastActive();
    void SaveOrUpdate(LocalUser user);
    void Remove(string identityUserId);
    void SetLastActiveId(string identityUserId);
    void SetPin(string identityUserId, string? pin);
    bool VerifyPin(string identityUserId, string pin);
    bool IsSingleUserMode { get; set; }
}
