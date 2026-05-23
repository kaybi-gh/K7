using K7.Shared.Dtos.Notifications;

namespace K7.Shared.Interfaces;

public interface INotificationAdminService
{
    Task<List<NotificationRuleDto>> GetNotificationRulesAsync(CancellationToken cancellationToken = default);
    Task<NotificationRuleDto> GetNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateNotificationRuleAsync(CreateNotificationRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateNotificationRuleAsync(Guid id, UpdateNotificationRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TestNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<NotificationEventDescriptorDto>> GetAvailableEventsAsync(CancellationToken cancellationToken = default);
}
