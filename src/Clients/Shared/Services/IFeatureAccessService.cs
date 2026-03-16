using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Services;

public interface IFeatureAccessService
{
    Task<bool> HasCapabilityAsync(Capability capability);
    Task<string?> GetRoleAsync();
}
