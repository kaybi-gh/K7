using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Common.Interfaces;

public interface IHomeLayoutMaintenanceService
{
    Task RemoveLibraryReferencesAsync(Guid deletedLibraryId, CancellationToken cancellationToken = default);

    Task<HomeLayoutDto> SanitizeAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default);
}
