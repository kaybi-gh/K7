using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateContentRestrictionProfileRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public RestrictionMatchCondition MatchCondition { get; init; }
    public required IReadOnlyList<ContentRestrictionRuleRequest> Rules { get; init; }
}

public sealed record UpdateContentRestrictionProfileRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public RestrictionMatchCondition MatchCondition { get; init; }
    public required IReadOnlyList<ContentRestrictionRuleRequest> Rules { get; init; }
}

public sealed record ContentRestrictionRuleRequest
{
    public RestrictionField Field { get; init; }
    public RestrictionOperator Operator { get; init; }
    public string? Value { get; init; }
}
