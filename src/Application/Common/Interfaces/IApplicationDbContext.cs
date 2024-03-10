using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<BaseMedia> Medias { get; }
    DbSet<BaseMetadata> Metadatas { get; }
    DbSet<ExternalId> ExternalIds { get; }
    DbSet<BaseRating> Ratings { get; }
    DbSet<MediaPicture> MediaPictures { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
