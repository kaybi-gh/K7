using System.Reflection.Emit;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<BaseMedia>
{
    public void Configure(EntityTypeBuilder<BaseMedia> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<MusicAlbum>(MediaType.MusicAlbum)
            .HasValue<MusicArtist>(MediaType.MusicArtist)
            .HasValue<MusicTrack>(MediaType.MusicTrack)
            .HasValue<Serie>(MediaType.Serie)
            .HasValue<SerieEpisode>(MediaType.SerieEpisode)
            .HasValue<SerieSeason>(MediaType.SerieSeason);

        builder
            .HasMany(m => m.IndexedFiles)
            .WithOne(i => i.Media)
            .HasForeignKey(i => i.MediaId);

        builder
            .HasOne(m => m.Metadata)
            .WithOne(m => m.Media)
            .HasForeignKey<BaseMetadata>(m => m.MediaId);
    }

    public static void Configure(EntityTypeBuilder<Movie> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicAlbum> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicArtist> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicTrack> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<Serie> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<SerieEpisode> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<SerieSeason> builder)
    {
    }
}
