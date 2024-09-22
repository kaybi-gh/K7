using System.Reflection.Emit;
using MediaServer.Domain.Entities.Metadatas.Files;
using MediaServer.Domain.Entities.Metadatas.Files.Tracks;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class FileTrackConfiguration : IEntityTypeConfiguration<BaseFileTrack>
{
    public void Configure(EntityTypeBuilder<BaseFileTrack> builder)
    {
        //builder.HasKey(x => new { x.FileMetadataId, x.Type, x.Index });

        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<AudioFileTrack>(FileTrackType.Audio)
            .HasValue<VideoFileTrack>(FileTrackType.Video);
    }

    public void Configure(EntityTypeBuilder<AudioFileTrack> builder)
    {
    }

    public void Configure(EntityTypeBuilder<VideoFileTrack> builder)
    {
    }
}
