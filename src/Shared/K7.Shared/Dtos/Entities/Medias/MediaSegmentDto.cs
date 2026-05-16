using K7.Shared.Enums;

namespace K7.Shared.Dtos.Entities.Medias;

public sealed record MediaSegmentDto
{
    public required MediaSegmentType Type { get; init; }
    public required long StartMs { get; init; }
    public required long EndMs { get; init; }
}
