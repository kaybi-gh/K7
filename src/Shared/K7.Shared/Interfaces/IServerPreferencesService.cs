using K7.Shared.Dtos.Home;

namespace K7.Shared.Interfaces;

public interface IServerPreferencesService
{
    Task<HomeLayoutDto?> GetServerHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task<HomeLayoutDto> GetEffectiveServerHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task UpdateServerHomeLayoutAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default);
    Task DeleteServerHomeLayoutAsync(CancellationToken cancellationToken = default);
}
