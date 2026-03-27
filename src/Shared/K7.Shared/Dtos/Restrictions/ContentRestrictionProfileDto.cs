using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Restrictions;

public sealed record ContentRestrictionProfileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RestrictionMatchCondition MatchCondition { get; init; }
    public required IReadOnlyList<ContentRestrictionRuleDto> Rules { get; init; }
    public required int UserCount { get; init; }

}

public sealed record ContentRestrictionRuleDto
{
    public required RestrictionField Field { get; init; }
    public required RestrictionOperator Operator { get; init; }
    public string? Value { get; init; }
}
