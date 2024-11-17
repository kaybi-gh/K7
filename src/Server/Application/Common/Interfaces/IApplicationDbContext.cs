using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace K7.Server.Application.Common.Interfaces;

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
