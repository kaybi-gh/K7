using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IDownloadService
{
    Task<DownloadDto> PrepareDownloadAsync(PrepareDownloadRequest request, CancellationToken cancellationToken = default);
    Task<DownloadDto> GetDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task DeleteDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    string GetDownloadFileUrl(Guid downloadId);
}
