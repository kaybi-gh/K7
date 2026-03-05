using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class FileMetadataConfiguration : IEntityTypeConfiguration<BaseFileMetadata>
{
    public void Configure(EntityTypeBuilder<BaseFileMetadata> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<AudioFileMetadata>(FileType.Audio)
            .HasValue<VideoFileMetadata>(FileType.Video);

        builder
            .HasMany(x => x.HlsSegments)
            .WithOne()
            .HasForeignKey(x => x.FileMetadataId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public static void Configure(EntityTypeBuilder<AudioFileMetadata> builder)
    {
        builder
            .HasOne(x => x.AudioTrack)
            .WithOne()
            .HasForeignKey<AudioFileTrack>(x => x.AudioFileMetadataId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public static void Configure(EntityTypeBuilder<VideoFileMetadata> builder)
    {
        builder
            .HasMany(x => x.AudioTracks)
            .WithOne()
            .HasForeignKey(x => x.VideoFileMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.VideoTracks)
            .WithOne()
            .HasForeignKey(x => x.VideoFileMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.SubtitleTracks)
            .WithOne()
            .HasForeignKey(x => x.VideoFileMetadataId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
