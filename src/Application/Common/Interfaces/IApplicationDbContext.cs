using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<BaseMedia> Medias { get; }
    DbSet<BaseMediaMetadata> Metadatas { get; }
    DbSet<MetadataPicture> MetadataPictures { get; }
    DbSet<Person> Persons { get; }
    DbSet<BasePersonRole> PersonRoles { get; }
    DbSet<BaseRating> Ratings { get; }
    DbSet<ExternalId> ExternalIds { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
