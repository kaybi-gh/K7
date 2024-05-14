using MediaServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class MetadataPictureConfiguration : IEntityTypeConfiguration<MetadataPicture>
{
    public void Configure(EntityTypeBuilder<MetadataPicture> builder)
    {
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

        builder
            .HasOne(mp => mp.Metadata)
            .WithMany(m => m.Pictures)
            .HasForeignKey(mp => mp.MetadataId)
            .IsRequired(false);

        builder.Property(m => m.OriginalRemoteUri)
            .HasConversion(v => v.ToString(), v => new Uri(v));
    }
}
