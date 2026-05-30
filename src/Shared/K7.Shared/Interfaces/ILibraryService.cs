using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface ILibraryService
{
    Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default);
    Task<List<LibraryGroupDto>> GetLibraryGroupsAsync(CancellationToken cancellationToken = default);
    Task<List<LibraryStatisticsDto>> GetLibraryStatisticsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default);
    Task UpdateLibraryAsync(Guid id, UpdateLibraryRequest request, CancellationToken cancellationToken = default);
    Task DeleteLibraryAsync(Guid id, CancellationToken cancellationToken = default);
    Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default);
    Task<List<MetadataProviderInfoDto>> GetMetadataProvidersAsync(LibraryMediaType? mediaType = null, CancellationToken cancellationToken = default);
    Task<Guid> UploadLibraryGroupCoverAsync(Guid libraryGroupId, Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<Guid> SetLibraryGroupCoverFromPictureAsync(Guid libraryGroupId, Guid sourcePictureId, CancellationToken cancellationToken = default);
    Task<List<LibraryPictureDto>> GetLibraryPicturesAsync(Guid libraryId, CancellationToken cancellationToken = default);
    Task UpdateLibraryGroupAsync(Guid id, UpdateLibraryGroupRequest request, CancellationToken cancellationToken = default);
    Task DeleteLibraryGroupAsync(Guid id, CancellationToken cancellationToken = default);
}
