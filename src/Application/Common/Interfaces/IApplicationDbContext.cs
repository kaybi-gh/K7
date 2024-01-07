using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Library> Libraries { get; }

    DbSet<BaseMedia> BaseMedias { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
