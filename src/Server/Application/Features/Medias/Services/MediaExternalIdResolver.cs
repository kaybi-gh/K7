using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Services;

public class MediaExternalIdResolver(
    IApplicationDbContext context,
    IServiceProvider serviceProvider,
    ILogger<MediaExternalIdResolver> logger)
{
    public async Task<ExternalId?> ResolveAsync(BaseMedia media, Library library, CancellationToken cancellationToken = default)
    {
        var existing = media.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, library.MetadataProviderName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var providerName = library.MetadataProviderName;
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        var identification = await GetIdentificationAsync(media, library, cancellationToken);
        if (identification is null)
        {
            logger.LogWarning("Cannot resolve external id for media {MediaId}: no identification source available", media.Id);
            return null;
        }

        var providerExternalId = await SearchProviderAsync(media, providerName, identification, cancellationToken);
        if (string.IsNullOrWhiteSpace(providerExternalId))
        {
            logger.LogWarning(
                "Cannot resolve external id for media {MediaId}: provider {Provider} returned no match for {Title}",
                media.Id,
                providerName,
                identification.Title);
            return null;
        }

        var externalId = new ExternalId
        {
            ProviderName = providerName,
            Value = providerExternalId,
            MediaId = media.Id
        };
        media.ExternalIds.Add(externalId);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved external id for media {MediaId}: {Provider}={ExternalId}",
            media.Id,
            providerName,
            providerExternalId);

        return externalId;
    }

    private async Task<MediaIdentification?> GetIdentificationAsync(
        BaseMedia media,
        Library library,
        CancellationToken cancellationToken)
    {
        var indexedFiles = await MediaLibraryLinkageHelper.GetIndexedFilesQuery(context, media)
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        foreach (var indexedFile in indexedFiles)
        {
            if (indexedFile.Identification is not null)
                return indexedFile.Identification;

            var derived = DeriveIdentificationFromFile(media, library, indexedFile);
            if (derived is not null)
                return derived;
        }

        return await DeriveIdentificationFromMedia(media, cancellationToken);
    }

    private static MediaIdentification? DeriveIdentificationFromFile(
        BaseMedia media,
        Library library,
        IndexedFile indexedFile)
    {
        return media switch
        {
            Movie when indexedFile.TryIdentifyMovie(out var movieIdentification) => movieIdentification,
            Serie when indexedFile.TryIdentifySerieEpisode(library, [indexedFile]) => new MediaIdentification(
                indexedFile.Identification?.SeriesTitle ?? indexedFile.Identification?.Title ?? string.Empty)
            {
                SeriesTitle = indexedFile.Identification?.SeriesTitle ?? indexedFile.Identification?.Title,
                ReleaseYear = indexedFile.Identification?.ReleaseYear
            },
            MusicAlbum when indexedFile.TryIdentifyMusicTrack(library, [indexedFile]) => new MediaIdentification(
                indexedFile.Identification?.AlbumName ?? indexedFile.Identification?.Title ?? string.Empty)
            {
                AlbumName = indexedFile.Identification?.AlbumName,
                ArtistName = indexedFile.Identification?.ArtistName,
                ReleaseYear = indexedFile.Identification?.ReleaseYear
            },
            _ => null
        };
    }

    private async Task<MediaIdentification?> DeriveIdentificationFromMedia(
        BaseMedia media,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(media.Title))
            return null;

        var identification = new MediaIdentification(media.Title)
        {
            ReleaseYear = media.ReleaseDate
        };

        switch (media)
        {
            case Serie:
                identification.SeriesTitle = media.Title;
                break;
            case MusicAlbum album:
                identification.AlbumName = media.Title;
                if (album.ArtistId is not null)
                {
                    identification.ArtistName = await context.Medias.OfType<MusicArtist>()
                        .Where(a => a.Id == album.ArtistId)
                        .Select(a => a.Title)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                break;
        }

        return identification;
    }

    private async Task<string?> SearchProviderAsync(
        BaseMedia media,
        string providerName,
        MediaIdentification identification,
        CancellationToken cancellationToken)
    {
        return media switch
        {
            Movie => await serviceProvider
                .GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(providerName)
                .SearchAsync(identification, cancellationToken),
            Serie => await serviceProvider
                .GetRequiredKeyedService<ISerieMetadataProvider>(providerName)
                .SearchSerieAsync(identification, cancellationToken),
            MusicAlbum => await serviceProvider
                .GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(providerName)
                .SearchAsync(identification, cancellationToken),
            _ => null
        };
    }
}
