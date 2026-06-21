using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetMediaGenresQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public HashSet<MediaType>? MediaTypes { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public HashSet<GenreOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public enum GenreOrderingOption
{
    MediaCountAsc,
    MediaCountDesc,
    UserPlayCountAsc,
    UserPlayCountDesc
}
