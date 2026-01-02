using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public record GetMediasWithPaginationQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? Ids { get; init; }
    // TODO - public bool? Seen { get; init; }
    public HashSet<MediaType>? MediaTypes { get; init; }
    public HashSet<MediaOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}
