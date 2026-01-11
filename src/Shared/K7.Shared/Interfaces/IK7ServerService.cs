using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IK7ServerService
{
    HttpClient HttpClient { get; }
    Uri? GetAbsoluteUri(string? relativePath = null);
    Task<Guid> CreateDeviceAsync(CreateDeviceRequest request);
    Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<List<MediaFormatDto>> GetMediaFormatsAsync();
    Task<MovieDto?> GetMovieAsync(Guid id);
    Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query);
    Task<PersonDto?> GetPersonAsync(Guid id);
}
