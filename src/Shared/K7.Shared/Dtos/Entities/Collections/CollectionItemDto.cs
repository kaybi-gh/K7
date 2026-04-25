using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Dtos.Entities.Collections;

public sealed record CollectionItemDto
{
    public Guid Id { get; init; }
    public Guid CollectionId { get; init; }
    public int Order { get; init; }
    public required LiteMediaDto Media { get; init; }
}
