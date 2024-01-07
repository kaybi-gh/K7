using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class BaseMediaConfiguration : IEntityTypeConfiguration<BaseMedia>
{
    public void Configure(EntityTypeBuilder<BaseMedia> builder)
    {
        builder
            .HasDiscriminator(m => m.Type)
            .HasValue<Episode>(MediaType.Episode)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<MusicAlbum>(MediaType.MusicAlbum)
            .HasValue<Season>(MediaType.Season)
            .HasValue<Serie>(MediaType.Serie)
            .HasValue<Track>(MediaType.Track);
    }
}
