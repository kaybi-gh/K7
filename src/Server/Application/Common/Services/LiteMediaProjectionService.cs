using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
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

    public LiteMediaDto ToLite(BaseMedia media, IReadOnlyDictionary<Guid, int>? serieSeasonCounts = null) =>
        media.ToLiteMediaDto(serieSeasonCounts);

    public async Task<List<LiteMediaDto>> ToLiteListAsync(
        IEnumerable<BaseMedia> medias,
        CancellationToken cancellationToken = default)
    {
        var list = medias.ToList();
        var counts = await GetSerieSeasonCountsAsync(list, cancellationToken);
        return list.Select(m => m.ToLiteMediaDto(counts)).ToList();
    }
}
