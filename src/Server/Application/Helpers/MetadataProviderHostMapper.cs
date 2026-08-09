using K7.Server.Application.Common;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Maps remote picture hosts and provider aliases to canonical <see cref="MetadataProviderNames"/> keys.
/// </summary>
public static class MetadataProviderHostMapper
{
    public static string FromHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return MetadataProviderNames.Local;

        var h = host.Trim().ToLowerInvariant();

        if (h is "image.tmdb.org" or "www.themoviedb.org" or "api.themoviedb.org")
            return MetadataProviderNames.Tmdb;

        if (h is "artworks.thetvdb.com" or "api4.thetvdb.com" or "thetvdb.com" or "www.thetvdb.com")
            return MetadataProviderNames.Tvdb;

        if (h is "coverartarchive.org" or "www.coverartarchive.org"
            or "archive.org" or "www.archive.org"
            || h.EndsWith(".archive.org", StringComparison.Ordinal))
            return MetadataProviderNames.CoverArt;

        if (h is "musicbrainz.org" or "www.musicbrainz.org")
            return MetadataProviderNames.MusicBrainz;

        if (h is "www.wikidata.org" or "wikidata.org")
            return MetadataProviderNames.Wikidata;

        if (h is "commons.wikimedia.org" or "upload.wikimedia.org"
            || h.EndsWith(".wikipedia.org", StringComparison.Ordinal))
            return MetadataProviderNames.Wikimedia;

        return MetadataProviderNames.Local;
    }

    public static string FromUri(Uri? uri) => FromHost(uri?.Host);

    public static string NormalizeProviderName(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return MetadataProviderNames.Local;

        var name = providerName.Trim().ToLowerInvariant();
        return name switch
        {
            "themoviedb" or "tmdb" => MetadataProviderNames.Tmdb,
            "imdb" => MetadataProviderNames.Tmdb,
            "thetvdb" or "tvdb" => MetadataProviderNames.Tvdb,
            "musicbrainz" or "mb" => MetadataProviderNames.MusicBrainz,
            "wikidata" => MetadataProviderNames.Wikidata,
            "wikimedia" or "commons" => MetadataProviderNames.Wikimedia,
            "coverart" or "coverartarchive" => MetadataProviderNames.CoverArt,
            "federation" => MetadataProviderNames.Local,
            _ => name
        };
    }

    /// <summary>
    /// Admission key for a background task. Required for <see cref="BackgroundTaskLane.Metadata"/>;
    /// always null for other lanes.
    /// </summary>
    public static string? NormalizeForBackgroundTask(BackgroundTaskLane lane, string? metadataProviderName)
    {
        if (lane != BackgroundTaskLane.Metadata)
            return null;

        if (string.IsNullOrWhiteSpace(metadataProviderName))
            throw new ArgumentException(
                "MetadataProviderName is required for Metadata-lane background tasks.",
                nameof(metadataProviderName));

        return metadataProviderName.Trim().ToLowerInvariant();
    }
}
