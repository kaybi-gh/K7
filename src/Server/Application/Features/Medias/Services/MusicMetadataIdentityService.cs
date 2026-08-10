using K7.Server.Application.Common;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Services;

public sealed record MusicAlbumIdentityMatch(
    string ProviderName,
    string ExternalId,
    string? ArtistMusicBrainzId,
    string? PreferredReleaseId);

/// <summary>
/// Resolves MusicBrainz album identity from embedded tag MBIDs first, then provider search.
/// </summary>
public class MusicMetadataIdentityService(
    IServiceProvider serviceProvider,
    ILogger<MusicMetadataIdentityService> logger)
{
    public async Task<MusicAlbumIdentityMatch?> ResolveAlbumAsync(
        MediaIdentification identification,
        string? libraryProviderName,
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken = default)
    {
        var providerName = MetadataProviderHostMapper.NormalizeProviderName(libraryProviderName);
        if (string.IsNullOrWhiteSpace(providerName)
            || string.Equals(providerName, MetadataProviderNames.Auto, StringComparison.OrdinalIgnoreCase))
            providerName = MetadataProviderNames.MusicBrainz;

        var artistMbid = FirstNonEmpty(
            identification.MusicBrainzAlbumArtistId,
            identification.MusicBrainzArtistId);

        if (!string.IsNullOrWhiteSpace(identification.MusicBrainzReleaseGroupId))
        {
            return new MusicAlbumIdentityMatch(
                providerName,
                identification.MusicBrainzReleaseGroupId.Trim(),
                artistMbid,
                FirstNonEmpty(identification.MusicBrainzReleaseId));
        }

        var searchIdentification = CloneIdentification(identification);
        if (!string.IsNullOrWhiteSpace(artistMbid))
            searchIdentification.MusicBrainzAlbumArtistId = artistMbid;

        var externalId = await SearchProviderAsync(
            searchIdentification,
            providerName,
            language,
            fallbackLanguage,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(externalId))
        {
            logger.LogDebug(
                "Music identity search returned no match for album {Album} / artist {Artist}",
                identification.AlbumName ?? identification.Title,
                identification.ArtistName);
            return null;
        }

        return new MusicAlbumIdentityMatch(
            providerName,
            externalId,
            artistMbid,
            FirstNonEmpty(identification.MusicBrainzReleaseId));
    }

    public async Task<string?> ResolveArtistIdAsync(
        string? artistName,
        string? knownMusicBrainzId,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(knownMusicBrainzId))
            return knownMusicBrainzId.Trim();

        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        var providers = serviceProvider.GetServices<IMusicArtistMetadataProvider>();
        var mb = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, MetadataProviderNames.MusicBrainz, StringComparison.OrdinalIgnoreCase));
        if (mb is null)
            return null;

        var details = await mb.SearchByNameAsync(artistName, language, cancellationToken);
        return details?.MusicBrainzArtistId;
    }

    private async Task<string?> SearchProviderAsync(
        MediaIdentification identification,
        string providerName,
        string? language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = serviceProvider.GetRequiredKeyedService<IMetadataProvider<ExternalMusicAlbumMetadata>>(providerName);
            return await provider.SearchAsync(identification, language, fallbackLanguage, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Music identity provider {Provider} search failed", providerName);
            return null;
        }
    }

    private static MediaIdentification CloneIdentification(MediaIdentification source) =>
        new(source.Title)
        {
            AlbumName = source.AlbumName,
            ArtistName = source.ArtistName,
            ReleaseYear = source.ReleaseYear,
            TrackNumber = source.TrackNumber,
            MusicBrainzReleaseId = source.MusicBrainzReleaseId,
            MusicBrainzReleaseGroupId = source.MusicBrainzReleaseGroupId,
            MusicBrainzArtistId = source.MusicBrainzArtistId,
            MusicBrainzAlbumArtistId = source.MusicBrainzAlbumArtistId,
            MusicBrainzRecordingId = source.MusicBrainzRecordingId,
            ProviderName = source.ProviderName,
            ProviderExternalId = source.ProviderExternalId
        };

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
