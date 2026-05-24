using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Restrictions;

public sealed record ContentRestrictionProfileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RuleGroupDto RuleFilter { get; init; }
    public required int UserCount { get; init; }
}
