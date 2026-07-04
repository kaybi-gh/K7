using K7.Server.Domain.Entities;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;

public interface IFileIndexer
{
    Task<LibraryScanResult> IndexAsync(Library library, CancellationToken cancellationToken = default);

    Task<LibraryScanResult> IndexPathsAsync(Library library, IReadOnlyList<string> paths, CancellationToken cancellationToken = default);
}
