using System.Text.Json;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ContentRestrictionProfileConfiguration : IEntityTypeConfiguration<ContentRestrictionProfile>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<ContentRestrictionProfile> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.Property(p => p.RuleFilter)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<RuleGroup>(v, JsonOptions)!);

        builder.HasMany(p => p.Users)
            .WithOne(u => u.ContentRestrictionProfile)
            .HasForeignKey(u => u.ContentRestrictionProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
