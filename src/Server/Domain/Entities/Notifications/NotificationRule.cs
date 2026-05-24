using K7.Server.Domain.Common;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Entities.Notifications;

public class NotificationRule : BaseAuditableEntity
{
    public required string Name { get; set; }
    public bool IsEnabled { get; set; }
    public NotificationProviderType ProviderType { get; set; }
    public NotificationPayloadFormat PayloadFormat { get; set; }
    public List<string> EventTypeNames { get; set; } = [];
    public required string ProviderConfig { get; set; }
    public string? TitleTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public string? RawJsonTemplate { get; set; }
    public RuleGroup? RuleFilter { get; set; }
}
