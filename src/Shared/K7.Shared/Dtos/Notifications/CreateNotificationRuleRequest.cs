using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Notifications;

public sealed record CreateNotificationRuleRequest
{
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string PayloadFormat { get; init; }
    public required IReadOnlyList<string> EventTypeNames { get; init; }
    public required string ProviderConfig { get; init; }
    public string? TitleTemplate { get; init; }
    public string? BodyTemplate { get; init; }
    public string? RawJsonTemplate { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
    public bool IsEnabled { get; init; } = true;
}
