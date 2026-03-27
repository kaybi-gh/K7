using K7.Server.Domain.Entities;
using K7.Server.Application.Common.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MetadataPictureConfiguration : IEntityTypeConfiguration<MetadataPicture>
{
    private readonly PathsConfiguration? _pathsConfiguration;

    public MetadataPictureConfiguration() { }
    public MetadataPictureConfiguration(PathsConfiguration? pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration;
    }

    public void Configure(EntityTypeBuilder<MetadataPicture> builder)
    {
        builder
            .HasOne(mp => mp.Media)
            .WithMany(m => m.Pictures)
            .HasForeignKey(mp => mp.MediaId)
            .IsRequired(false);

        builder
            .HasOne(mp => mp.VideoFileMetadata)
            .WithOne(m => m.Thumbnails)
            .HasForeignKey<MetadataPicture>(mp => mp.VideoFileMetadataId)
            .OnDelete(DeleteBehavior.Cascade)
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

        builder
            .HasOne(mp => mp.Playlist)
            .WithOne(p => p.CoverPicture)
            .HasForeignKey<MetadataPicture>(mp => mp.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.Property(m => m.OriginalRemoteUri)
            .HasConversion(
                v => v != null ? v.ToString() : null,
                v => v != null ? new Uri(v) : null
            );

        if (_pathsConfiguration is not null)
        {
            builder.Property(p => p.LocalPath)
                .HasConversion(
                    v => v != null ? _pathsConfiguration.ToRelativeMetadataPath(v) : null,
                    v => v != null ? _pathsConfiguration.ResolveMetadataPath(v) : null
                );
        }
    }
}
