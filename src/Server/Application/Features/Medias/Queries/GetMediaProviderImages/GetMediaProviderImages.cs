using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaProviderImages;

public record GetMediaProviderImagesQuery(Guid MediaId) : IRequest<IReadOnlyList<ProviderImageDto>>;

public class GetMediaProviderImagesQueryHandler(
    IApplicationDbContext context,
    IEnumerable<IMetadataImageProvider> imageProviders,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<GetMediaProviderImagesQuery, IReadOnlyList<ProviderImageDto>>
{
    public async Task<IReadOnlyList<ProviderImageDto>> Handle(GetMediaProviderImagesQuery request, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var language = await ResolveLanguageAsync(media, cancellationToken);
        var mediaType = media.Type;
        int? seasonNumber = null;
        int? episodeNumber = null;

        // For child entities, resolve the parent's provider ID
        var externalIds = media.ExternalIds;

        if (media is SerieSeason season)
        {
            seasonNumber = season.SeasonNumber;
            var serie = await context.Medias
                .AsNoTracking()
                .Include(m => m.ExternalIds)
                .FirstOrDefaultAsync(m => m.Id == season.SerieId, cancellationToken);
            if (serie is not null)
                externalIds = serie.ExternalIds;
        }
        else if (media is SerieEpisode episode)
        {
            episodeNumber = episode.EpisodeNumber;
            var seasonEntity = await context.Medias
                .OfType<SerieSeason>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == episode.SeasonId, cancellationToken);
            if (seasonEntity is not null)
                seasonNumber = seasonEntity.SeasonNumber;

            var serie = await context.Medias
                .AsNoTracking()
                .Include(m => m.ExternalIds)
                .FirstOrDefaultAsync(m => m.Id == episode.SerieId, cancellationToken);
            if (serie is not null)
                externalIds = serie.ExternalIds;
        }

        var results = new List<ProviderImageDto>();

        foreach (var provider in imageProviders)
        {
            if (!provider.SupportsMediaType(mediaType))
                continue;

            var externalId = externalIds.FirstOrDefault(e => e.ProviderName == provider.ProviderName);
            if (externalId is null)
                continue;

            var providerContext = new ImageProviderContext(
                mediaType,
                externalId.Value,
                language,
                seasonNumber,
                episodeNumber);

            var images = await provider.GetImagesAsync(providerContext, cancellationToken);
            results.AddRange(images);
        }

        return MetadataImageUrlHelper.FilterProviderImages(results);
    }

    private async Task<string> ResolveLanguageAsync(BaseMedia media, CancellationToken cancellationToken)
    {
        var library = await MediaLibraryLinkageHelper.FindLibraryAsync(context, media, cancellationToken);
        return library?.MetadataLanguage ?? "en";
    }
}
