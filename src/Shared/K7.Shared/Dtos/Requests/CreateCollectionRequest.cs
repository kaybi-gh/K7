using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateCollectionRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
    public MediaType? MediaType { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}
