using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetMediaTagsQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public MediaType[]? MediaTypes { get; init; }
    public MetadataTagKind[]? Kinds { get; init; }
    public string? SearchText { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public MediaTagOrderingOption[]? OrderBy { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 100;
    public int Limit { get; init; } = 100;
}

public enum MediaTagOrderingOption
{
    MediaCountAsc,
    MediaCountDesc,
    UserPlayCountAsc,
    UserPlayCountDesc
}
