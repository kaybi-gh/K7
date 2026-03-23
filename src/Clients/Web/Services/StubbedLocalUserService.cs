using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Web.Services;

public class NullLocalUserService : ILocalUserService
{
    public List<LocalUser> GetAll() => [];
    public LocalUser? GetLastActive() => null;
    public void SaveOrUpdate(LocalUser user) { }
    public void Remove(string identityUserId) { }
    public void SetLastActiveId(string identityUserId) { }
    public void SetPin(string identityUserId, string? pin) { }
    public bool VerifyPin(string identityUserId, string pin) => true;
    public bool IsSingleUserMode { get => true; set { } }
}
