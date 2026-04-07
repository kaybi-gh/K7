using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;
public interface IFileIndexer
{
    public Task<LibraryScanResult> IndexAsync(Library library, CancellationToken cancellationToken);
}
