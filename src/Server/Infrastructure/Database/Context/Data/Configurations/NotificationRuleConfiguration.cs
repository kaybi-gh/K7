using System.Text.Json;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.EventTypeNames)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
            .HasColumnType("text")
            .IsRequired()
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => a!.SequenceEqual(b!),
                c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                c => c.ToList()));

        builder.Property(r => r.ProviderConfig)
            .IsRequired();

        builder.Property(r => r.RuleFilter)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<RuleGroup>(v, JsonOptions));

        builder.HasIndex(r => r.IsEnabled)
            .HasDatabaseName("IX_NotificationRules_IsEnabled");
    }
}
