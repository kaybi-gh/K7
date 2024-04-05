using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<BaseMedia> Medias { get; }
    DbSet<BaseMediaMetadata> Metadatas { get; }
    DbSet<ExternalId> ExternalIds { get; }
    DbSet<BaseRating> Ratings { get; }
    DbSet<MetadataPicture> MetadataPictures { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
