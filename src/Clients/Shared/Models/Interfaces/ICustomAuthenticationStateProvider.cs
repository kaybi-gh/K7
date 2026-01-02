using System.Security.Claims;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface ICustomAuthenticationStateProvider
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
