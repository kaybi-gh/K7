using K7.Server.Domain.Entities;
using K7.Server.Application.Common.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MetadataPictureVariantConfiguration : IEntityTypeConfiguration<MetadataPictureVariant>
{
    private readonly PathsConfiguration? _pathsConfiguration;

    public MetadataPictureVariantConfiguration() { }
    public MetadataPictureVariantConfiguration(PathsConfiguration? pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration;
    }

    public void Configure(EntityTypeBuilder<MetadataPictureVariant> builder)
    {
        builder
            .HasOne(v => v.MetadataPicture)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.MetadataPictureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(v => new { v.MetadataPictureId, v.Size })
            .IsUnique();

        if (_pathsConfiguration is not null)
        {
            builder.Property(v => v.LocalPath)
                .HasConversion(
                    v => _pathsConfiguration.ToRelativeMetadataPath(v),
                    v => _pathsConfiguration.ResolveMetadataPath(v)
                );
        }
    }
}
