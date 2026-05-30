using System.Reflection;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Notifications;
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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<LibraryGroup> LibraryGroups => Set<LibraryGroup>();
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
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();
    public DbSet<AudioAnalysis> AudioAnalysis => Set<AudioAnalysis>();
    public DbSet<MediaSegment> MediaSegments => Set<MediaSegment>();
    public DbSet<MusicArtistCredit> MusicArtistCredits => Set<MusicArtistCredit>();
    public DbSet<ServerSetting> ServerSettings => Set<ServerSetting>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<UserCapabilityOverride> UserCapabilityOverrides => Set<UserCapabilityOverride>();
    public DbSet<UserLibraryExclusion> UserLibraryExclusions => Set<UserLibraryExclusion>();
    public DbSet<UserMediaExclusion> UserMediaExclusions => Set<UserMediaExclusion>();
    public DbSet<ContentRestrictionProfile> ContentRestrictionProfiles => Set<ContentRestrictionProfile>();
    public DbSet<MediaRecommendation> MediaRecommendations => Set<MediaRecommendation>();
    public DbSet<LibraryScanIssue> ScanIssues => Set<LibraryScanIssue>();
    public DbSet<Download> Downloads => Set<Download>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<EphemeralStreamToken> EphemeralStreamTokens => Set<EphemeralStreamToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly(),
            type => type != typeof(MetadataPictureConfiguration)
                 && type != typeof(MetadataPictureVariantConfiguration));

        new MetadataPictureConfiguration(_pathsConfiguration).Configure(builder.Entity<MetadataPicture>());
        new MetadataPictureVariantConfiguration(_pathsConfiguration).Configure(builder.Entity<MetadataPictureVariant>());

        // SQLite cannot ORDER BY DateTimeOffset natively.
        // Store as ISO 8601 TEXT which sorts lexicographically in chronological order.
        if (Database.IsSqlite())
        {
            var converter = new ValueConverter<DateTimeOffset, string>(
                v => v.ToString("O"),
                v => DateTimeOffset.Parse(v));

            var nullableConverter = new ValueConverter<DateTimeOffset?, string?>(
                v => v.HasValue ? v.Value.ToString("O") : null,
                v => v != null ? DateTimeOffset.Parse(v) : null);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(converter);
                        property.SetColumnType("TEXT");
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(nullableConverter);
                        property.SetColumnType("TEXT");
                    }
                }
            }
        }
    }
}
