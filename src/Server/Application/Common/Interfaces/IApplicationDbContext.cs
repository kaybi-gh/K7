using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace K7.Server.Application.Common.Interfaces;

public interface IApplicationDbContext // How to put this into domain?
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
