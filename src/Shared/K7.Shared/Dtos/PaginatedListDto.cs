namespace K7.Shared.Dtos;

public sealed record PaginatedListDto<T>
{
    public IReadOnlyCollection<T>? Items { get; init; }
    public int? PageNumber { get; init; }
    public int? TotalPages { get; init; }
    public int? TotalCount { get; init; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
