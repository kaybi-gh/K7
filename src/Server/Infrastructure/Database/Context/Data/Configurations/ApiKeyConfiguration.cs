using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.Property(k => k.Name).HasMaxLength(200).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(12).IsRequired();

        builder.HasIndex(k => k.KeyHash).IsUnique();
    }
}
