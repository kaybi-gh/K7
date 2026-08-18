using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Helpers;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public enum ImageDisplayRole
{
    Thumb,
    Card,
    Hero
}

public static class MetadataPictureDisplayHelper
{
    /// <summary>
    /// Max CSS pixels * DPR before a hero backdrop upgrades from Medium to original.
    /// </summary>
    public const int HeroBackdropPixelBudget = 1920;

    public static MetadataPictureSize? SizeFor(ImageDisplayRole role) => role switch
    {
        ImageDisplayRole.Thumb => MetadataPictureSize.Small,
        ImageDisplayRole.Card => MetadataPictureSize.Medium,
        ImageDisplayRole.Hero => null,
        _ => MetadataPictureSize.Small
    };

    /// <summary>
    /// Capped backdrop for typical windows; pair with original via
    /// <see cref="ResolveAdaptiveBackdropUrls"/> when the source is larger.
    /// </summary>
    public static MetadataPictureSize? SizeForHeroBackdrop() => MetadataPictureSize.Medium;

    public static (string? DisplayUrl, string? HighResUrl) ResolveAdaptiveBackdropUrls(
        MetadataPictureDto? picture,
        IK7ServerService apiClient,
        DateTimeOffset? cacheVersion = null)
    {
        if (picture?.Uri is null || picture.Type != MetadataPictureType.Backdrop)
            return (null, null);

        var displayUri = apiClient.GetAbsoluteUri(
            picture.GetUri(SizeForHeroBackdrop())?.OriginalString)?.AbsoluteUri;
        var displayUrl = MediaPictureUrlHelper.WithCacheBuster(displayUri, cacheVersion);

        if (picture.OriginalWidth is not > HeroBackdropPixelBudget)
            return (displayUrl, null);

        var highResUri = apiClient.GetAbsoluteUri(picture.GetUri()?.OriginalString)?.AbsoluteUri;
        var highResUrl = MediaPictureUrlHelper.WithCacheBuster(highResUri, cacheVersion);
        if (string.Equals(displayUrl, highResUrl, StringComparison.Ordinal))
            return (displayUrl, null);

        return (displayUrl, highResUrl);
    }

    public static MetadataPictureSize? GetBestDisplaySize(
        MetadataPictureDto picture,
        params MetadataPictureSize[] preferredSizes)
    {
        if (picture.AvailableSizes.Count > 0)
        {
            foreach (var size in preferredSizes)
            {
                if (picture.AvailableSizes.Contains(size))
                    return size;
            }

            return picture.AvailableSizes[0];
        }

        return null;
    }

    public static bool IsHdStill(MetadataPictureDto? picture)
    {
        if (picture is null || picture.Type != MetadataPictureType.Still)
            return false;

        if (picture.OriginalWidth is > 0
            && picture.OriginalHeight is > 0)
        {
            return MetadataPictureThresholds.MeetsHdStillThreshold(
                picture.OriginalWidth.Value,
                picture.OriginalHeight.Value);
        }

        // Unknown dimensions: assume usable until proven otherwise.
        return true;
    }

    /// <summary>
    /// TV hero full-bleed: blur square covers, and stills that are below the HD threshold.
    /// Movie/serie backdrops and HD stills stay sharp.
    /// </summary>
    public static bool ShouldSoftenTvHeroBackdrop(MediaType? mediaType, MetadataPictureDto? heroPicture)
    {
        if (mediaType is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist)
            return true;

        if (heroPicture?.Type != MetadataPictureType.Still)
            return false;

        return !IsHdStill(heroPicture);
    }
}
