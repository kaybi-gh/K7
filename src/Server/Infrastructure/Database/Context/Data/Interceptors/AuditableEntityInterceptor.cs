using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace K7.Server.Infrastructure.Database.Context.Data.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IUser _user;
    private readonly TimeProvider _dateTime;

    public AuditableEntityInterceptor(
        IUser user,
        TimeProvider dateTime)
    {
        _user = user;
        _dateTime = dateTime;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void UpdateEntities(DbContext? context)
    {
        if (context is null)
            return;

        var hasAuditableChanges = false;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.State == EntityState.Deleted)
            {
                hasAuditableChanges = true;
                break;
            }

            if (entry.State == EntityState.Added
                || entry.State == EntityState.Modified
                || entry.HasChangedOwnedEntities())
            {
                hasAuditableChanges = true;
                break;
            }
        }

        if (!hasAuditableChanges)
            return;

        var utcNow = _dateTime.GetUtcNow();
        var identityId = _user.IdentityId;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                entry.Entity.LastModifiedBy = identityId;
                entry.Entity.LastModified = utcNow;

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedBy = identityId;
                    entry.Entity.Created = utcNow;
                }
            }
        }
    }
}

public static class Extensions
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null &&
            r.TargetEntry.Metadata.IsOwned() &&
            (r.TargetEntry.State == EntityState.Added || r.TargetEntry.State == EntityState.Modified));
}
