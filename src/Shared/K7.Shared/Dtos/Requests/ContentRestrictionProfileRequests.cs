using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateContentRestrictionProfileRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RuleGroupDto RuleFilter { get; init; }
}

public sealed record UpdateContentRestrictionProfileRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RuleGroupDto RuleFilter { get; init; }
}
