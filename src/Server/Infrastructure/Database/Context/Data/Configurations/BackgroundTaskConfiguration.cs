using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class BackgroundTaskConfiguration : IEntityTypeConfiguration<BackgroundTask>
{
    public void Configure(EntityTypeBuilder<BackgroundTask> builder)
    {
        // Mirrors the scheduler ordering exactly: eligible rows filtered on Status, then WorkClass
        // descending, Priority descending, Created ascending. WorkClass values are the scheduling
        // weights, so no computed column is needed and the sort stays index-backed.
        builder.HasIndex(t => new { t.Status, t.WorkClass, t.Priority, t.Created })
            .HasDatabaseName("IX_BackgroundTasks_Status_WorkClass_Priority_Created");

        // Supports the on-demand boost, which raises the pending tasks of a single media.
        builder.HasIndex(t => t.TargetEntityId)
            .HasDatabaseName("IX_BackgroundTasks_TargetEntityId");

        builder.HasIndex(t => t.Lane)
            .HasDatabaseName("IX_BackgroundTasks_Lane");

        // Enqueue deduplication must be atomic: a watcher event and a scheduled scan can race on the
        // same media. The unique filtered index turns the check-then-insert into a conflict.
        builder.HasIndex(t => new { t.Name, t.TargetEntityId })
            .IsUnique()
            // Pending, InProgress, WaitingForRetry. Double-quoted identifiers are valid on both
            // Postgres and SQLite, so the same filter serves both providers.
            .HasFilter("\"Status\" IN (0, 1, 2)")
            .HasDatabaseName("UX_BackgroundTasks_Name_TargetEntityId_Active");
    }
}
