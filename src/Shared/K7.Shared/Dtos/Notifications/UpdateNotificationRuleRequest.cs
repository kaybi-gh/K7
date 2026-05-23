namespace K7.Shared.Dtos.Notifications;

public sealed record UpdateNotificationRuleRequest
{
    public required string Name { get; init; }
    public required string ProviderType { get; init; }
    public required string EventTypeName { get; init; }
    public required string ProviderConfig { get; init; }
    public string? PayloadTemplate { get; init; }
    public string? Conditions { get; init; }
    public string? ConditionsLogic { get; init; }
    public bool IsEnabled { get; init; }
}
