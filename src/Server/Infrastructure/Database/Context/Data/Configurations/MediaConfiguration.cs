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
}

public class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
    }
}

public class MusicAlbumConfiguration : IEntityTypeConfiguration<MusicAlbum>
{
    public void Configure(EntityTypeBuilder<MusicAlbum> builder)
    {
        builder
            .HasMany(a => a.Tracks)
            .WithOne(t => t.Album)
            .HasForeignKey(t => t.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MusicTrackConfiguration : IEntityTypeConfiguration<MusicTrack>
{
    public void Configure(EntityTypeBuilder<MusicTrack> builder)
    {
    }
}

public class SerieConfiguration : IEntityTypeConfiguration<Serie>
{
    public void Configure(EntityTypeBuilder<Serie> builder)
    {
        builder
            .HasMany(s => s.Seasons)
            .WithOne(ss => ss.Serie)
            .HasForeignKey(ss => ss.SerieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SerieEpisodeConfiguration : IEntityTypeConfiguration<SerieEpisode>
{
    public void Configure(EntityTypeBuilder<SerieEpisode> builder)
    {
        builder
            .HasOne(e => e.Serie)
            .WithMany()
            .HasForeignKey(e => e.SerieId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasIndex(e => new { e.SeasonId, e.EpisodeNumber });
    }
}

public class SerieSeasonConfiguration : IEntityTypeConfiguration<SerieSeason>
{
    public void Configure(EntityTypeBuilder<SerieSeason> builder)
    {
        builder
            .HasMany(ss => ss.Episodes)
            .WithOne(e => e.Season)
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(ss => new { ss.SerieId, ss.SeasonNumber });
    }
}
