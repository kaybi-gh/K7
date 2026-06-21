using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetMediaBrowseFacetsQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public MediaType[]? MediaTypes { get; init; }
}
