using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Rules;

public sealed record RuleFieldDescriptorDto
{
    public required string FieldName { get; init; }
    public required string DisplayName { get; init; }
    public required RuleFieldValueType ValueType { get; init; }
    public required IReadOnlyList<RuleOperator> Operators { get; init; }
    public IReadOnlyList<RuleFieldOptionDto>? Options { get; init; }
}

public sealed record RuleFieldOptionDto
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

public enum RuleFieldValueType
{
    Text,
    Number,
    Date,
    Boolean,
    Select,
}
