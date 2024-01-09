using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaServer.Infrastructure.Context.Data.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<IndexedFile>
{
    public void Configure(EntityTypeBuilder<IndexedFile> builder)
    {
        /*builder
            .HasDiscriminator(m => m.Type)
            .HasValue<SerieEpisode>(FileType.Episode)
            .HasValue<Movie>(FileType.Movie)
            .HasValue<MusicAlbum>(FileType.Track);*/
    }
}
