using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Context.Data.Configurations;

public class MetadataPictureConfiguration : IEntityTypeConfiguration<MetadataPicture>
{
    public void Configure(EntityTypeBuilder<MetadataPicture> builder)
    {
        builder
            .HasOne(mp => mp.Metadata)
            .WithMany(m => m.Pictures)
            .HasForeignKey(mp => mp.MetadataId)
            .IsRequired(false);

        builder
            .HasOne(mp => mp.VideoFileMetadata)
            .WithMany(m => m.Thumbnails)
            .HasForeignKey(mp => mp.VideoFileMetadataId)
            .IsRequired(false);

        builder
            .HasOne(mp => mp.Person)
            .WithOne(p => p.PortraitPicture)
            .HasForeignKey<MetadataPicture>(mp => mp.PersonId)
            .IsRequired(false);

        builder
            .HasOne(mp => mp.PersonRole)
            .WithOne(pr => pr.PortraitPicture)
            .HasForeignKey<MetadataPicture>(mp => mp.PersonRoleId)
            .IsRequired(false);

        builder.Property(m => m.OriginalRemoteUri)
            .HasConversion(
                v => v != null ? v.ToString() : null,
                v => v != null ? new Uri(v) : null
            );
    }
}
