using System.Reflection;
using System.Reflection.Emit;
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

    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<BaseMedia> BaseMedias => Set<BaseMedia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<BaseMedia>()
            .HasDiscriminator(x => x.Type)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<Season>(MediaType.Season)
            .HasValue<Episode>(MediaType.Episode)
            .HasValue<MusicAlbum>(MediaType.MusicAlbum)
            .HasValue<Track>(MediaType.Track);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
