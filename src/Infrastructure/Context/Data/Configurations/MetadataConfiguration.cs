using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class MetadataConfiguration : IEntityTypeConfiguration<BaseMediaMetadata>
{
    public void Configure(EntityTypeBuilder<BaseMediaMetadata> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<MovieMetadata>(MediaType.Movie)
            .HasValue<MusicAlbumMetadata>(MediaType.MusicAlbum)
            .HasValue<MusicTrackMetadata>(MediaType.MusicTrack)
            .HasValue<SerieMetadata>(MediaType.Serie)
            .HasValue<SerieEpisodeMetadata>(MediaType.SerieEpisode)
            .HasValue<SerieSeasonMetadata>(MediaType.SerieSeason);
    }

    public static void Configure(EntityTypeBuilder<MovieMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicAlbumMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicTrackMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<SerieMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<SerieEpisodeMetadata> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<SerieSeasonMetadata> builder)
    {
    }
}
