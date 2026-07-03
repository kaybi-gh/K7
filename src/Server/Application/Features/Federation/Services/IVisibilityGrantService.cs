using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public interface IVisibilityGrantService
{
    Task<IReadOnlyList<FederationVisibilityGrantDto>> GetGlobalShareGrantsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task SetGlobalShareGrantsAsync(
        Guid ownerUserId,
        IReadOnlyList<FederationVisibilityGrantDto> grants,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FederationVisibilityGrantDto>> GetContentGrantsAsync(
        Guid ownerUserId,
        Guid? playlistId,
        Guid? collectionId,
        CancellationToken cancellationToken = default);

    Task SetContentGrantsAsync(
        Guid ownerUserId,
        Guid? playlistId,
        Guid? collectionId,
        FederationContentType? contentType,
        IReadOnlyList<FederationVisibilityGrantDto> grants,
        CancellationToken cancellationToken = default);
}
