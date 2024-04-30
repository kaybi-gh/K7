namespace MediaClient.Shared.Domain.Models;

public record GetMediasWithPaginationQuery
{
    public int? LibraryId { get; init; }
    public MediaType? MediaType { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}
