using K7.Server.Domain.Entities.Notifications;

namespace K7.Server.Domain.Events;

public class NotificationRuleDeletedEvent(NotificationRule rule) : BaseEvent
{
    public NotificationRule Rule { get; } = rule;
}
