using MediaServer.Domain.Entities.Medias;
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

        builder.HasMany(m => m.IndexedFiles)
               .WithMany();

        builder.HasOne(m => m.Library)
               .WithMany(l => l.Medias)
               .HasForeignKey(m => m.LibraryId);
    }

    public static void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<MusicAlbum> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<MusicArtist> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<MusicTrack> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<Serie> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<SerieEpisode> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }

    public static void Configure(EntityTypeBuilder<SerieSeason> builder)
    {
        builder.HasOne(m => m.Metadata)
               .WithOne();
    }
}
