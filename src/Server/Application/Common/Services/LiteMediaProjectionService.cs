using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Common.Services;

public sealed class LiteMediaProjectionService(IApplicationDbContext context)
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetSerieSeasonCountsAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default) =>
        await SerieSeasonCountHelper.GetCountsBySerieIdsAsync(
            context,
            SerieSeasonCountHelper.ExtractSerieIdsFromMedias(medias),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>> GetPictureSizesAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default) =>
        await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(
            context,
            MetadataPictureSizesHelper.ExtractPictureIdsFromMedias(medias),
            cancellationToken);

    public LiteMediaDto ToLite(
        BaseMedia media,
        IReadOnlyDictionary<Guid, int>? serieSeasonCounts = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null) =>
        media.ToLiteMediaDto(serieSeasonCounts, pictureSizes);

    public async Task<List<LiteMediaDto>> ToLiteListAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default)
    {
        var list = medias.ToList();
        var counts = await GetSerieSeasonCountsAsync(list, cancellationToken);
        var pictureSizes = await GetPictureSizesAsync(list, cancellationToken);
        return list.Select(m => m.ToLiteMediaDto(counts, pictureSizes)).ToList();
    }
}
