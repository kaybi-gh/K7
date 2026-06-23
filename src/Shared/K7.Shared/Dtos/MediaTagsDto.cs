using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record MediaTagValueDto
{
    public required string DisplayName { get; init; }
    public int MediaCount { get; init; }
}

public sealed record MediaTagKindValuesDto
{
    public MetadataTagKind Kind { get; init; }
    public IReadOnlyList<MediaTagValueDto> Values { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

public sealed record MediaTagsDto
{
    public IReadOnlyList<MediaTagKindValuesDto> Kinds { get; init; } = [];
}
