using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedPlaylistDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType MediaType { get; init; }
    public IReadOnlyList<FederatedPlaylistItemDto> Items { get; init; } = [];
}

public sealed record FederatedPlaylistItemDto
{
    public required FederatedMediaRef Media { get; init; }
    public int Order { get; init; }
}

public sealed record FederatedSmartPlaylistDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType MediaType { get; init; }
    public RuleGroupDto RuleFilter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
}
