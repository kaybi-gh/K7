using K7.Clients.Shared.Domain.Enums;

namespace K7.Clients.Shared.Domain.Models;

public record GetLiteMediasQuery
{
    public int? LibraryId { get; init; }
    public MediaType? MediaType { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}
