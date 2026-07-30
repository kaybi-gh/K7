using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateDynamicPlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public RuleGroupDto RuleFilter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}

public sealed record UpdateDynamicPlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public RuleGroupDto RuleFilter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
}
