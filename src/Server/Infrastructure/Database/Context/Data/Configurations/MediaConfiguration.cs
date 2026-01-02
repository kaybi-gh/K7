using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<BaseMedia>
{
    public void Configure(EntityTypeBuilder<BaseMedia> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<MusicAlbum>(MediaType.MusicAlbum)
            .HasValue<MusicTrack>(MediaType.MusicTrack)
            .HasValue<Serie>(MediaType.Serie)
            .HasValue<SerieEpisode>(MediaType.SerieEpisode)
            .HasValue<SerieSeason>(MediaType.SerieSeason);

        builder
            .HasIndex(e => e.Slug)
            .IsUnique();

        builder
            .HasMany(m => m.IndexedFiles)
            .WithOne(i => i.Media)
            .HasForeignKey(i => i.MediaId);
    }

    public static void Configure(EntityTypeBuilder<Movie> builder)
    {
    }

    public static void Configure(EntityTypeBuilder<MusicAlbum> builder)
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
