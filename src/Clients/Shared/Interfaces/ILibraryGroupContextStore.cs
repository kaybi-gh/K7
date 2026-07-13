using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Interfaces;

public interface ILibraryGroupContextStore
{
    event Action<Guid>? Changed;

    Task<LibraryGroupContextSnapshot?> EnsureContextAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<MediaTagsDto?> EnsureTagsAsync(
        Guid groupId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default);

    void Invalidate(Guid groupId);
}
