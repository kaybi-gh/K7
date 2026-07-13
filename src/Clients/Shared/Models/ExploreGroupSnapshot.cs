using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Models;

public sealed class ExploreGroupSnapshot
{
    public required LibraryGroupDto Group { get; init; }

    public IReadOnlyList<MediaTagValueDto> Genres { get; init; } = [];
}
