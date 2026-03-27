using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface ILibraryService
{
    Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default);
    Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default);
}
