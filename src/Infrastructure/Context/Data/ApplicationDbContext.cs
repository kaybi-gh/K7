using System.Reflection;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Infrastructure.Context.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediaServer.Infrastructure.Context.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<IndexedFile> IndexedFiles => Set<IndexedFile>();
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<BaseMedia> Medias => Set<BaseMedia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<BaseMedia>()
            .HasDiscriminator(x => x.Type)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<MusicAlbum>(MediaType.MusicAlbum)
            .HasValue<MusicArtist>(MediaType.MusicArtist)
            .HasValue<MusicTrack>(MediaType.MusicTrack)
            .HasValue<Serie>(MediaType.Serie)
            .HasValue<SerieSeason>(MediaType.SerieSeason)
            .HasValue<SerieEpisode>(MediaType.SerieEpisode);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
