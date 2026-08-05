using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IClientAppPasswordUserService
{
    Task<List<ClientAppPasswordDto>> GetClientAppPasswordsAsync(CancellationToken cancellationToken = default);
    Task<CreateClientAppPasswordResponse> CreateClientAppPasswordAsync(string name, CancellationToken cancellationToken = default);
    Task RevokeClientAppPasswordAsync(Guid id, CancellationToken cancellationToken = default);
}
