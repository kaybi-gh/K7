using K7.Server.Domain.Entities.Metadatas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MetadataTagConfiguration : IEntityTypeConfiguration<MetadataTag>
{
    public void Configure(EntityTypeBuilder<MetadataTag> builder)
    {
        builder.HasIndex(t => new { t.Kind, t.NormalizedKey }).IsUnique();
        builder.Property(t => t.NormalizedKey).HasMaxLength(256);
        builder.Property(t => t.DisplayName).HasMaxLength(512);
    }
}

public class MediaMetadataTagConfiguration : IEntityTypeConfiguration<MediaMetadataTag>
{
    public void Configure(EntityTypeBuilder<MediaMetadataTag> builder)
    {
        builder.HasKey(t => new { t.MediaId, t.MetadataTagId });

        builder
            .HasOne(t => t.Media)
            .WithMany(m => m.MetadataTags)
            .HasForeignKey(t => t.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(t => t.MetadataTag)
            .WithMany(t => t.MediaAssignments)
            .HasForeignKey(t => t.MetadataTagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.MetadataTagId);
    }
}
