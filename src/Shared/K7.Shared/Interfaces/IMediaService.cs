using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IMediaService
{
    Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default);
    Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaDto?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<HomeFeedItemDto>?> GetHomeFeedAsync(GetHomeFeedQuery query, CancellationToken cancellationToken = default);
    Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<PersonDto>?> GetPersonsAsync(GetPersonsWithPaginationQuery query, CancellationToken cancellationToken = default);
    Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, K7.Server.Domain.Enums.MediaType? mediaType = null, CancellationToken cancellationToken = default);
    Task ReidentifyIndexedFileAsync(Guid id, ReidentifyIndexedFileRequest request, CancellationToken cancellationToken = default);
    Task ReidentifyMediaAsync(Guid id, ReidentifyMediaRequest request, CancellationToken cancellationToken = default);
    Task RefreshMediaMetadataAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LiteSerieEpisodeDto?> GetNextEpisodeAsync(Guid serieId, Guid currentEpisodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegmentsAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
