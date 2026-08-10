using K7.Server.Application.Common;
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
    MusicMetadataIdentityService musicIdentityService,
    ILogger<MediaExternalIdResolver> logger)
{
    public async Task<ExternalId?> ResolveAsync(BaseMedia media, Library library, CancellationToken cancellationToken = default)
    {
        if (media is Serie)
            return await ResolveSerieAsync(media, library, cancellationToken);

        if (media is MusicArtist artist)
            return await ResolveMusicArtistAsync(artist, library, cancellationToken);

        if (media is MusicAlbum)
            return await ResolveMusicAlbumAsync(media, library, cancellationToken);

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

        var providerExternalId = await SearchProviderAsync(
            media,
            providerName,
            identification,
            library.MetadataLanguage,
            library.MetadataFallbackLanguage,
            cancellationToken);
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

    private async Task<ExternalId?> ResolveMusicAlbumAsync(BaseMedia media, Library library, CancellationToken cancellationToken)
    {
        var providerName = MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName);
        if (string.IsNullOrWhiteSpace(providerName)
            || string.Equals(providerName, MetadataProviderNames.Auto, StringComparison.OrdinalIgnoreCase))
            providerName = MetadataProviderNames.MusicBrainz;

        var existing = media.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var identification = await GetIdentificationAsync(media, library, cancellationToken);
        if (identification is null)
        {
            logger.LogWarning("Cannot resolve external id for media {MediaId}: no identification source available", media.Id);
            return null;
        }

        await EnrichMusicIdentificationFromTagsAsync(identification, media, cancellationToken);

        var match = await musicIdentityService.ResolveAlbumAsync(
            identification,
            providerName,
            library.MetadataLanguage,
            library.MetadataFallbackLanguage,
            cancellationToken);
        if (match is null)
        {
            logger.LogWarning(
                "Cannot resolve external id for media {MediaId}: music identity returned no match for {Title}",
                media.Id,
                identification.Title);
            return null;
        }

        var externalId = new ExternalId
        {
            ProviderName = match.ProviderName,
            Value = match.ExternalId,
            MediaId = media.Id
        };
        media.ExternalIds.Add(externalId);

        if (!string.IsNullOrWhiteSpace(match.PreferredReleaseId)
            && !media.ExternalIds.Any(e => e.ProviderName == "musicbrainz-release"))
        {
            media.ExternalIds.Add(new ExternalId
            {
                ProviderName = "musicbrainz-release",
                Value = match.PreferredReleaseId,
                MediaId = media.Id
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved external id for media {MediaId}: {Provider}={ExternalId}",
            media.Id,
            match.ProviderName,
            match.ExternalId);

        return externalId;
    }

    private async Task<ExternalId?> ResolveMusicArtistAsync(MusicArtist artist, Library library, CancellationToken cancellationToken)
    {
        var existing = artist.ExternalIds.FirstOrDefault(e =>
            string.Equals(e.ProviderName, MetadataProviderNames.MusicBrainz, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var artistId = await musicIdentityService.ResolveArtistIdAsync(
            artist.Title,
            knownMusicBrainzId: null,
            library.MetadataLanguage ?? MetadataProviderNames.DefaultLanguage,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(artistId))
        {
            logger.LogWarning(
                "Cannot resolve external id for music artist {MediaId}: no MusicBrainz match for {Title}",
                artist.Id,
                artist.Title);
            return null;
        }

        var externalId = new ExternalId
        {
            ProviderName = MetadataProviderNames.MusicBrainz,
            Value = artistId,
            MediaId = artist.Id
        };
        artist.ExternalIds.Add(externalId);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved external id for media {MediaId}: {Provider}={ExternalId}",
            artist.Id,
            MetadataProviderNames.MusicBrainz,
            artistId);

        return externalId;
    }

    private async Task EnrichMusicIdentificationFromTagsAsync(
        MediaIdentification identification,
        BaseMedia media,
        CancellationToken cancellationToken)
    {
        if (identification.MusicBrainzReleaseGroupId is not null
            || identification.MusicBrainzReleaseId is not null)
            return;

        var file = await MediaLibraryLinkageHelper.GetIndexedFilesQuery(context, media)
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null)
            return;

        var tagReader = serviceProvider.GetService<IAudioTagReader>();
        var tags = tagReader?.ReadTags(file.Path, includeCoverArt: false);
        if (tags is null)
            return;

        identification.MusicBrainzReleaseId ??= tags.MusicBrainzReleaseId;
        identification.MusicBrainzReleaseGroupId ??= tags.MusicBrainzReleaseGroupId;
        identification.MusicBrainzArtistId ??= tags.MusicBrainzArtistId;
        identification.MusicBrainzAlbumArtistId ??= tags.MusicBrainzAlbumArtistId;
        identification.MusicBrainzRecordingId ??= tags.MusicBrainzRecordingId;
    }

    private async Task<ExternalId?> ResolveSerieAsync(BaseMedia media, Library library, CancellationToken cancellationToken)
    {
        var serie = media as Serie;
        var cascade = SerieMetadataProviderCascade.ResolveSearchProviders(library.MetadataProviderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preferred = !string.IsNullOrWhiteSpace(serie?.NumberingProviderName)
            ? MetadataProviderHostMapper.NormalizeProviderName(serie.NumberingProviderName)
            : MetadataProviderHostMapper.NormalizeProviderName(library.MetadataProviderName);
        if (SerieMetadataProviderCascade.IsAuto(preferred))
            preferred = MetadataProviderNames.Tmdb;

        var existing = media.ExternalIds
            .Where(e => cascade.Contains(MetadataProviderNames.Normalize(
                MetadataProviderHostMapper.NormalizeProviderName(e.ProviderName))))
            .OrderByDescending(e => string.Equals(
                MetadataProviderNames.Normalize(MetadataProviderHostMapper.NormalizeProviderName(e.ProviderName)),
                preferred,
                StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        if (existing is not null)
            return existing;

        var identification = await GetIdentificationAsync(media, library, cancellationToken);
        if (identification is null)
        {
            logger.LogWarning("Cannot resolve external id for media {MediaId}: no identification source available", media.Id);
            return null;
        }

        var identityService = serviceProvider.GetService<SerieMetadataIdentityService>();
        if (identityService is not null)
        {
            var match = await identityService.ResolveAsync(
                identification,
                library.MetadataProviderName,
                [identification],
                library.MetadataLanguage,
                library.MetadataFallbackLanguage,
                cancellationToken);
            if (match is not null)
            {
                foreach (var (providerName, value) in match.ExternalIds)
                {
                    if (media.ExternalIds.Any(e =>
                            string.Equals(e.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    media.ExternalIds.Add(new ExternalId
                    {
                        ProviderName = providerName,
                        Value = value,
                        MediaId = media.Id
                    });
                }

                if (serie is not null)
                    serie.NumberingProviderName = match.NumberingProviderName;

                await context.SaveChangesAsync(cancellationToken);

                var primary = media.ExternalIds.FirstOrDefault(e =>
                    string.Equals(e.ProviderName, match.NumberingProviderName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Value, match.NumberingExternalId, StringComparison.OrdinalIgnoreCase))
                    ?? media.ExternalIds.FirstOrDefault(e =>
                        string.Equals(e.ProviderName, match.NumberingProviderName, StringComparison.OrdinalIgnoreCase));

                logger.LogInformation(
                    "Resolved external id for media {MediaId}: {Provider}={ExternalId}",
                    media.Id,
                    match.NumberingProviderName,
                    match.NumberingExternalId);

                return primary;
            }
        }

        foreach (var providerKey in SerieMetadataProviderCascade.ResolveSearchProviders(library.MetadataProviderName))
        {
            var providerExternalId = await SearchProviderAsync(
                media,
                providerKey,
                identification,
                library.MetadataLanguage,
                library.MetadataFallbackLanguage,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(providerExternalId))
                continue;

            var provider = serviceProvider.GetRequiredKeyedService<ISerieMetadataProvider>(providerKey);
            var externalId = new ExternalId
            {
                ProviderName = provider.ProviderName,
                Value = providerExternalId,
                MediaId = media.Id
            };
            media.ExternalIds.Add(externalId);
            if (serie is not null)
                serie.NumberingProviderName = provider.ProviderName;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Resolved external id for media {MediaId}: {Provider}={ExternalId}",
                media.Id,
                provider.ProviderName,
                providerExternalId);

            return externalId;
        }

        logger.LogWarning(
            "Cannot resolve external id for media {MediaId}: no cascade provider matched {Title}",
            media.Id,
            identification.Title);
        return null;
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
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        return media switch
        {
            Movie => await serviceProvider
                .GetRequiredKeyedService<IMetadataProvider<ExternalMovieMetadata>>(providerName)
                .SearchAsync(identification, language, fallbackLanguage, cancellationToken),
            Serie => await serviceProvider
                .GetRequiredKeyedService<ISerieMetadataProvider>(providerName)
                .SearchSerieAsync(identification, language, fallbackLanguage, cancellationToken),
            MusicAlbum => await serviceProvider
                .GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(providerName)
                .SearchAsync(identification, language, fallbackLanguage, cancellationToken),
            _ => null
        };
    }
}
