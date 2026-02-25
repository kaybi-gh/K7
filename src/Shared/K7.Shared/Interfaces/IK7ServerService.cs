using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IK7ServerService
{
    HttpClient HttpClient { get; }
    Uri? GetAbsoluteUri(string? relativePath = null);
    Task<Guid> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);
    Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<DeviceDto>?> GetDevicesAsync(GetDevicesQuery? query = null, CancellationToken cancellationToken = default);
    Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default);
    Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default);
    Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IndexedFileStreamUri?> GetIndexedFileStreamUriAsync(GetIndexedFileStreamsUriQuery query, CancellationToken cancellationToken = default);
    Task<StreamingSessionDto?> CreateStreamSessionAsync(CreateStreamSessionRequest request, CancellationToken cancellationToken = default);
}
