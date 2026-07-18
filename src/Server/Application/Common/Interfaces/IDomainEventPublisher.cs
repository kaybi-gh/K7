using K7.Server.Domain.Common;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Publishes domain events that are not attached to an EF-tracked entity SaveChanges cycle
/// (e.g. background ffmpeg failures, health monitor transitions).
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(BaseEvent domainEvent, CancellationToken cancellationToken = default);
}
