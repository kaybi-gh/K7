using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Models;

public sealed class LibraryGroupContextSnapshot
{
    public required LibraryGroupDto Group { get; init; }

    public LibraryMediaType? MediaType => Group.MediaType;

    public IReadOnlyList<Guid> LibraryIds => Group.LibraryIds;
}
