using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<IndexedFile> IndexedFiles { get; }
    DbSet<Library> Libraries { get; }
    DbSet<BaseMedia> Medias { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
