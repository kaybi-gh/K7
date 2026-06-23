using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Federation;
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
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace K7.Server.Application.Common.Interfaces;

public interface IApplicationDbContext // How to put this into domain?
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<LibraryGroup> LibraryGroups { get; }
    DbSet<BaseMedia> Medias { get; }
    DbSet<BaseFileMetadata> FileMetadatas { get; }
    DbSet<BaseFileTrack> FileTracks { get; }
    DbSet<MetadataPicture> MetadataPictures { get; }
    DbSet<MetadataPictureVariant> MetadataPictureVariants { get; }
    DbSet<Person> Persons { get; }
    DbSet<BasePersonRole> PersonRoles { get; }
    DbSet<BaseRating> Ratings { get; }
    DbSet<ExternalId> ExternalIds { get; }
    DbSet<HlsSegment> HlsSegments { get; }
    DbSet<StreamSession> StreamSessions { get; }
    DbSet<BackgroundTask> BackgroundTasks { get; }
    DbSet<Device> Devices { get; }
    DbSet<User> Users { get; }
    DbSet<UserMediaState> UserMediaStates { get; }
    DbSet<MediaPlaybackSession> MediaPlaybackSessions { get; }
    DbSet<PlaybackSessionDetails> PlaybackSessionDetails { get; }
    DbSet<Collection> Collections { get; }
    DbSet<CollectionItem> CollectionItems { get; }
    DbSet<Playlist> Playlists { get; }
    DbSet<PlaylistItem> PlaylistItems { get; }
    DbSet<AudioAnalysis> AudioAnalysis { get; }
    DbSet<MediaSegment> MediaSegments { get; }
    DbSet<MusicArtistCredit> MusicArtistCredits { get; }
    DbSet<ServerSetting> ServerSettings { get; }
    DbSet<UserSetting> UserSettings { get; }
    DbSet<UserCapabilityOverride> UserCapabilityOverrides { get; }
    DbSet<UserLibraryExclusion> UserLibraryExclusions { get; }
    DbSet<UserMediaExclusion> UserMediaExclusions { get; }
    DbSet<ContentRestrictionProfile> ContentRestrictionProfiles { get; }
    DbSet<MediaRecommendation> MediaRecommendations { get; }
    DbSet<LibraryScanIssue> ScanIssues { get; }
    DbSet<Download> Downloads { get; }
    DbSet<NotificationRule> NotificationRules { get; }
    DbSet<EphemeralStreamToken> EphemeralStreamTokens { get; }
    DbSet<PeerServer> PeerServers { get; }
    DbSet<PeerShareAgreement> PeerShareAgreements { get; }
    DbSet<PeerRequest> PeerRequests { get; }
    DbSet<RemoteIndexedFile> RemoteIndexedFiles { get; }
    DbSet<MetadataTag> MetadataTags { get; }
    DbSet<MediaMetadataTag> MediaMetadataTags { get; }
    DbSet<ApiKey> ApiKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
