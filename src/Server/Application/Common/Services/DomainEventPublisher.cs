using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Publishes domain events that are not attached to an EF SaveChanges cycle. Safe to call from
/// singleton services: creates a fresh DI scope per publish so scoped handlers resolve correctly.
/// </summary>
public class DomainEventPublisher(IServiceScopeFactory scopeFactory) : IDomainEventPublisher
{
    public async Task PublishAsync(BaseEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(domainEvent, cancellationToken);
    }
}
