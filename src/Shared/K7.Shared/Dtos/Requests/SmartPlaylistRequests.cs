using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateSmartPlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public SmartPlaylistMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<SmartPlaylistRuleRequest> Rules { get; init; } = [];
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; } = SmartPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}

public sealed record UpdateSmartPlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public SmartPlaylistMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<SmartPlaylistRuleRequest> Rules { get; init; } = [];
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
}

public sealed record SmartPlaylistRuleRequest
{
    public SmartPlaylistField Field { get; init; }
    public SmartPlaylistOperator Operator { get; init; }
    public string? Value { get; init; }
}
