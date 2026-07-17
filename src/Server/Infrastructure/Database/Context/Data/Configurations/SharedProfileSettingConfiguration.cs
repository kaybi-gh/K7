using K7.Server.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SharedProfileSettingConfiguration : IEntityTypeConfiguration<SharedProfileSetting>
{
    public void Configure(EntityTypeBuilder<SharedProfileSetting> builder)
    {
        builder.ToTable("SharedProfileSettings");

        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Value).IsRequired();

        builder
            .HasIndex(s => new { s.SharedProfileId, s.Key })
            .IsUnique();
    }
}
