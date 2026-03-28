using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class BackgroundTaskConfiguration : IEntityTypeConfiguration<BackgroundTask>
{
    public void Configure(EntityTypeBuilder<BackgroundTask> builder)
    {
        builder.HasIndex(t => new { t.Status, t.Priority, t.Created })
            .HasDatabaseName("IX_BackgroundTasks_Status_Priority_Created");

        builder.HasIndex(t => t.TargetEntityId)
            .HasDatabaseName("IX_BackgroundTasks_TargetEntityId");
    }
}
