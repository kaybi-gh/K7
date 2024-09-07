using System.Reflection;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.PersonRoles;
using MediaServer.Domain.Entities.Ratings;
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
    public DbSet<BaseMediaMetadata> Metadatas => Set<BaseMediaMetadata>();
    public DbSet<MetadataPicture> MetadataPictures => Set<MetadataPicture>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<BasePersonRole> PersonRoles => Set<BasePersonRole>();
    public DbSet<BaseRating> Ratings => Set<BaseRating>();
    public DbSet<ExternalId> ExternalIds => Set<ExternalId>();
    public DbSet<HlsSegment> HlsSegments => Set<HlsSegment>();
    public DbSet<BackgroundTask> BackgroundTasks => Set<BackgroundTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
