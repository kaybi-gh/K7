using System.Reflection;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Settings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Application.Common.Configuration;
using K7.Server.Infrastructure.Database.Context.Data.Configurations;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.Database.Context.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly PathsConfiguration? _pathsConfiguration;

    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IOptions<PathsConfiguration> pathsConfiguration) : base(options)
    {
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public DbSet<IndexedFile> IndexedFiles => Set<IndexedFile>();
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<BaseMedia> Medias => Set<BaseMedia>();
    public DbSet<BaseFileMetadata> FileMetadatas => Set<BaseFileMetadata>();
    public DbSet<MetadataPicture> MetadataPictures => Set<MetadataPicture>();
    public DbSet<MetadataPictureVariant> MetadataPictureVariants => Set<MetadataPictureVariant>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<BasePersonRole> PersonRoles => Set<BasePersonRole>();
    public DbSet<BaseRating> Ratings => Set<BaseRating>();
    public DbSet<ExternalId> ExternalIds => Set<ExternalId>();
    public DbSet<HlsSegment> HlsSegments => Set<HlsSegment>();
    public DbSet<StreamSession> StreamSessions => Set<StreamSession>();
    public DbSet<BaseFileTrack> FileTracks => Set<BaseFileTrack>();
    public DbSet<BackgroundTask> BackgroundTasks => Set<BackgroundTask>();
    public DbSet<Device> Devices => Set<Device>();
    public new DbSet<User> Users => Set<User>();
    public DbSet<UserMediaState> UserMediaStates => Set<UserMediaState>();
    public DbSet<MediaPlaybackSession> MediaPlaybackSessions => Set<MediaPlaybackSession>();
    public DbSet<PlaybackSessionDetails> PlaybackSessionDetails => Set<PlaybackSessionDetails>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();
    public DbSet<AudioAnalysis> AudioAnalysis => Set<AudioAnalysis>();
    public DbSet<ServerSetting> ServerSettings => Set<ServerSetting>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<UserCapabilityOverride> UserCapabilityOverrides => Set<UserCapabilityOverride>();
    public DbSet<UserLibraryExclusion> UserLibraryExclusions => Set<UserLibraryExclusion>();
    public DbSet<UserMediaExclusion> UserMediaExclusions => Set<UserMediaExclusion>();
    public DbSet<ContentRestrictionProfile> ContentRestrictionProfiles => Set<ContentRestrictionProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly(),
            type => type != typeof(MetadataPictureConfiguration)
                 && type != typeof(MetadataPictureVariantConfiguration));

        new MetadataPictureConfiguration(_pathsConfiguration).Configure(builder.Entity<MetadataPicture>());
        new MetadataPictureVariantConfiguration(_pathsConfiguration).Configure(builder.Entity<MetadataPictureVariant>());
    }
}
