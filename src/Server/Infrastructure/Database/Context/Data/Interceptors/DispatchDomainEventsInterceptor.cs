using System.Runtime.CompilerServices;
using K7.Server.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace K7.Server.Infrastructure.Database.Context.Data.Interceptors;

public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    // Options may resolve this interceptor once (captive). Pending events must be per DbContext.
    private static readonly ConditionalWeakTable<DbContext, List<BaseEvent>> PendingByContext = new();

    public DispatchDomainEventsInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CollectDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchPending(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchPending(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ClearPending(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ClearPending(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void CollectDomainEvents(DbContext? context)
    {
        if (context is null)
            return;

        // Must run before save: Deleted entities leave the tracker after SaveChanges,
        // which previously dropped UserDeleted / revoke / review-delete events.
        var pending = PendingByContext.GetOrCreateValue(context);

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.Entity.DomainEvents.Count == 0)
                continue;

            pending.AddRange(entry.Entity.DomainEvents);
            entry.Entity.ClearDomainEvents();
        }
    }

    private async Task DispatchPending(DbContext? context)
    {
        if (context is null || !PendingByContext.TryGetValue(context, out var pending) || pending.Count == 0)
            return;

        var events = pending.ToList();
        pending.Clear();

        foreach (var domainEvent in events)
            await _mediator.Publish(domainEvent);
    }

    private static void ClearPending(DbContext? context)
    {
        if (context is not null && PendingByContext.TryGetValue(context, out var pending))
            pending.Clear();
    }
}
