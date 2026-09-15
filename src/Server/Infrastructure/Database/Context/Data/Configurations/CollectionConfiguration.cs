using System.Text.Json;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder
            .Property(c => c.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(c => c.Description)
            .HasMaxLength(2000);

        builder
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder
            .HasOne(c => c.LibraryGroup)
            .WithMany()
            .HasForeignKey(c => c.LibraryGroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder
            .HasMany(c => c.Items)
            .WithOne(i => i.Collection)
            .HasForeignKey(i => i.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(c => c.RuleFilter)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<RuleGroup>(v, JsonOptions));
    }
}
