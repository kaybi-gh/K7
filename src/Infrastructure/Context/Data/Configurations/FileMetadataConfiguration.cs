using MediaServer.Domain.Entities.Metadatas.Files;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class FileMetadataConfiguration : IEntityTypeConfiguration<BaseFileMetadata>
{
    public void Configure(EntityTypeBuilder<BaseFileMetadata> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<AudioFileMetadata>(FileType.Audio)
            .HasValue<VideoFileMetadata>(FileType.Video);
    }

    public static void Configure(EntityTypeBuilder<AudioFileMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<VideoFileMetadata> builder)
    {
        builder
            .HasMany(x => x.HlsSegments)
            .WithOne()
            .HasForeignKey(x => x.VideoFileMetadataId);
    }
}
