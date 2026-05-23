using K7.Server.Domain.Common;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Notifications;

public class NotificationRule : BaseAuditableEntity
{
    public required string Name { get; set; }
    public bool IsEnabled { get; set; }
    public NotificationProviderType ProviderType { get; set; }
    public required string EventTypeName { get; set; }
    public required string ProviderConfig { get; set; }
    public string? PayloadTemplate { get; set; }
    public string? Conditions { get; set; }
    public string? ConditionsLogic { get; set; }
}
