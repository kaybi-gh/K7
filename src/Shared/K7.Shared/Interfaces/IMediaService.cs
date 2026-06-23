using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;

using K7.Shared.Enums;

namespace K7.Shared.Interfaces;

public interface IMediaService
{
    Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default);
    Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaDto?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<LiteMediaDto>?> QueryMediasAsync(QueryMediasRequest request, CancellationToken cancellationToken = default);
    Task<MediaTagsDto?> GetMediaTagsAsync(GetMediaTagsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>?> GetMediaBrowseFilterSuggestionsAsync(GetMediaBrowseFilterSuggestionsQuery query, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<HomeFeedItemDto>?> GetHomeFeedAsync(GetHomeFeedQuery query, CancellationToken cancellationToken = default);
    Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedListDto<PersonDto>?> GetPersonsAsync(GetPersonsWithPaginationQuery query, CancellationToken cancellationToken = default);
    Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, MediaType? mediaType = null, CancellationToken cancellationToken = default);
    Task ReidentifyIndexedFileAsync(Guid id, ReidentifyIndexedFileRequest request, CancellationToken cancellationToken = default);
    Task ReidentifyMediaAsync(Guid id, ReidentifyMediaRequest request, CancellationToken cancellationToken = default);
    Task RefreshMediaMetadataAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateMediaMetadataAsync(Guid id, UpdateMediaMetadataRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UploadMediaPictureAsync(Guid mediaId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default);
    Task DeleteMediaPictureAsync(Guid mediaId, Guid pictureId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderImageDto>> GetMediaProviderImagesAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<Guid> ImportMediaPictureFromUrlAsync(Guid mediaId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default);
    Task RefreshPersonMetadataAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdatePersonMetadataAsync(Guid id, UpdatePersonMetadataRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UploadPersonPictureAsync(Guid personId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default);
    Task DeletePersonPictureAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Guid> ImportPersonPictureFromUrlAsync(Guid personId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderImageDto>> GetPersonProviderImagesAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<LiteSerieEpisodeDto?> GetNextEpisodeAsync(Guid serieId, Guid currentEpisodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegmentsAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task DetectMediaSegmentsAsync(Guid seasonId, CancellationToken cancellationToken = default);
    Task<List<LiteMediaDto>> GetSimilarMediaAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiteMusicTrackDto>> GetArtistTopTracksAsync(Guid artistId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayedMusicTrackDto>> GetTopMusicTracksAsync(Guid[]? libraryIds = null, int count = 20, CancellationToken cancellationToken = default);
    Task<List<PersonKnownForItemDto>> GetPersonKnownForAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<SetMediaWatchStateResultDto?> SetMediaWatchStateAsync(Guid mediaId, bool watched, WatchStateScope scope = WatchStateScope.Item, CancellationToken cancellationToken = default);
}
