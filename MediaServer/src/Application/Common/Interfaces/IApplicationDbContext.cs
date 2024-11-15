using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Files;
using MediaServer.Domain.Entities.Metadatas.Files.Tracks;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.PersonRoles;
using MediaServer.Domain.Entities.Ratings;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext // How to put this into domain?
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<BaseMedia> Medias { get; }
    DbSet<BaseMediaMetadata> MediaMetadatas { get; }
    DbSet<BaseFileMetadata> FileMetadatas { get; }
    DbSet<BaseFileTrack> FileTracks { get; }
    DbSet<MetadataPicture> MetadataPictures { get; }
    DbSet<Person> Persons { get; }
    DbSet<BasePersonRole> PersonRoles { get; }
    DbSet<BaseRating> Ratings { get; }
    DbSet<ExternalId> ExternalIds { get; }
    DbSet<HlsSegment> HlsSegments { get; }
    DbSet<BackgroundTask> BackgroundTasks { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
