using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class FileTrackConfiguration : IEntityTypeConfiguration<BaseFileTrack>
{
    public void Configure(EntityTypeBuilder<BaseFileTrack> builder)
    {
        //builder.HasKey(x => new { x.FileMetadataId, x.Type, x.Index });
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<AudioFileTrack>(FileTrackType.Audio)
            .HasValue<VideoFileTrack>(FileTrackType.Video)
            .HasValue<SubtitleFileTrack>(FileTrackType.Subtitle);
    }

    public void Configure(EntityTypeBuilder<AudioFileTrack> builder)
    {
    }

    public void Configure(EntityTypeBuilder<VideoFileTrack> builder)
    {
    }
}
