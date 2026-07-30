using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Interfaces;

/// <summary>
/// Music album metadata providers that can use on-disk / existing track hints
/// when selecting a concrete MusicBrainz release inside a release-group.
/// </summary>
public interface IMusicAlbumReleaseAwareMetadataProvider : IMetadataProvider<ExternalMusicAlbumMetadata>
{
    Task<ExternalMusicAlbumMetadata> FetchMetadata(
        string providerId,
        string language,
        MusicAlbumReleaseHints? hints,
        CancellationToken cancellationToken = default);
}
