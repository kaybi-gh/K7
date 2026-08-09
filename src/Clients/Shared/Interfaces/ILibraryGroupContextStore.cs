using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Interfaces;

public interface ILibraryGroupContextStore
{
    /// <summary>Catalog membership changed for this library group (scan, batch add, etc.).</summary>
    event Action<Guid>? Changed;

    /// <summary>A single media's metadata or pictures changed within this library group scope.</summary>
    event Action<Guid, Guid>? MediaVisualChanged;

    Task<LibraryGroupContextSnapshot?> EnsureContextAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<MediaTagsDto?> EnsureTagsAsync(
        Guid groupId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default);

    void Invalidate(Guid groupId);
}
