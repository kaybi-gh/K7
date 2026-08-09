using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Builds the stable key identifying the media a set of indexed files resolves to.
/// </summary>
/// <remarks>
/// The key must match the criteria the creation handler itself uses to find an existing media, otherwise
/// two commands that would resolve to the same media could take different locks: album lookup is
/// (title, artist, year) inside a library, serie lookup is title plus year, movie lookup is
/// (title, release date). Falls back to the file path so an unidentified file still gets a distinct key
/// rather than colliding with every other unidentified file.
/// </remarks>
public static class MediaIdentityKey
{
    /// <summary>
    /// Builds the identity key of a media creation request.
    /// </summary>
    /// <param name="mediaType">Type of media being created.</param>
    /// <param name="libraryId">Library the media belongs to; identities never cross libraries.</param>
    /// <param name="indexedFiles">Files the request groups together.</param>
    public static string Build(MediaType mediaType, Guid libraryId, IReadOnlyList<IndexedFile> indexedFiles)
    {
        var primaryFile = indexedFiles.Count > 0 ? indexedFiles[0] : null;
        var identification = primaryFile?.Identification;

        var identity = mediaType switch
        {
            MediaType.MusicAlbum => BuildAlbumIdentity(identification, primaryFile),
            MediaType.Serie => BuildSerieIdentity(identification, primaryFile),
            _ => BuildMovieIdentity(identification, primaryFile)
        };

        return $"{libraryId:N}|{mediaType}|{identity}";
    }

    private static string BuildAlbumIdentity(MediaIdentification? identification, IndexedFile? primaryFile)
    {
        // Mirrors FindOrCreateAlbumAsync: album name, artist, year. Album name falls back to the parent
        // directory exactly like identification does, so tracks of one folder share a key.
        var album = Normalize(identification?.AlbumName) ?? Normalize(primaryFile?.ParentDirectory);
        var artist = Normalize(identification?.ArtistName);
        var year = identification?.ReleaseYear?.Year;

        return album is null
            ? FallbackToPath(primaryFile)
            : $"album:{album}|artist:{artist ?? "-"}|year:{year?.ToString() ?? "-"}";
    }

    private static string BuildSerieIdentity(MediaIdentification? identification, IndexedFile? primaryFile)
    {
        // Mirrors FindSerieByTitleAndYearAsync: title + year. Season and episode numbers must NOT take
        // part, otherwise two episodes of the same serie would take different locks and could both create
        // the serie.
        var serie = Normalize(identification?.SeriesTitle) ?? Normalize(identification?.Title);
        var year = identification?.ReleaseYear?.Year;

        return serie is null
            ? FallbackToPath(primaryFile)
            : $"serie:{serie}|year:{year?.ToString() ?? "-"}";
    }

    private static string BuildMovieIdentity(MediaIdentification? identification, IndexedFile? primaryFile)
    {
        var title = Normalize(identification?.Title);
        var year = identification?.ReleaseYear?.Year;

        return title is null
            ? FallbackToPath(primaryFile)
            : $"movie:{title}|year:{year?.ToString() ?? "-"}";
    }

    private static string FallbackToPath(IndexedFile? primaryFile)
        => $"path:{Normalize(primaryFile?.Path) ?? "unknown"}";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
