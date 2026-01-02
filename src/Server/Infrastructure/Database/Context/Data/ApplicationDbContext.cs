using System.Reflection;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<IndexedFile> IndexedFiles => Set<IndexedFile>();
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<BaseMedia> Medias => Set<BaseMedia>();
    public DbSet<BaseFileMetadata> FileMetadatas => Set<BaseFileMetadata>();
    public DbSet<MetadataPicture> MetadataPictures => Set<MetadataPicture>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<BasePersonRole> PersonRoles => Set<BasePersonRole>();
    public DbSet<BaseRating> Ratings => Set<BaseRating>();
    public DbSet<ExternalId> ExternalIds => Set<ExternalId>();
    public DbSet<HlsSegment> HlsSegments => Set<HlsSegment>();
    public DbSet<BaseFileTrack> FileTracks => Set<BaseFileTrack>();
    public DbSet<BackgroundTask> BackgroundTasks => Set<BackgroundTask>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
