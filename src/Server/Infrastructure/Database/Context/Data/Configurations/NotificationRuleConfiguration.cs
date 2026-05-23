using K7.Server.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.EventTypeName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.ProviderConfig)
            .IsRequired();

        builder.HasIndex(r => new { r.IsEnabled, r.EventTypeName })
            .HasDatabaseName("IX_NotificationRules_IsEnabled_EventTypeName");
    }
}
