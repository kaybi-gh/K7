using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetMediaBrowseFilterSuggestionsQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public MediaType[]? MediaTypes { get; init; }
    public required string Field { get; init; }
    public string? SearchText { get; init; }
    public int Limit { get; init; } = 20;
}
