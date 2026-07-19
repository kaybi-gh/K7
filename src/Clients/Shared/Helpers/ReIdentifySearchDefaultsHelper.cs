using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Helpers;

public static class ReIdentifySearchDefaultsHelper
{
    public static (string? Query, int? Year) FromIndexedFiles(
        IEnumerable<IndexedFileDto>? indexedFiles,
        MediaType mediaType,
        Guid? preferredIndexedFileId = null,
        string? fallbackQuery = null,
        int? fallbackYear = null)
    {
        if (indexedFiles is null)
            return (fallbackQuery, fallbackYear);

        var files = indexedFiles.ToList();
        if (files.Count == 0)
            return (fallbackQuery, fallbackYear);

        IEnumerable<IndexedFileDto> ordered = files;
        if (preferredIndexedFileId.HasValue)
        {
            ordered = files
                .Where(f => f.Id == preferredIndexedFileId.Value)
                .Concat(files.Where(f => f.Id != preferredIndexedFileId.Value));
        }

        foreach (var file in ordered)
        {
            var fromIdentification = FromIdentification(file.Identification, mediaType);
            if (!string.IsNullOrWhiteSpace(fromIdentification.Query))
                return fromIdentification;
        }

        return (fallbackQuery, fallbackYear);
    }

    public static (string? Query, int? Year) FromIdentification(
        MediaIdentificationDto? identification,
        MediaType mediaType)
    {
        if (identification is null)
            return (null, null);

        var query = mediaType switch
        {
            MediaType.Serie => FirstNonEmpty(identification.SeriesTitle, identification.Title),
            MediaType.MusicAlbum => FirstNonEmpty(identification.AlbumName, identification.Title),
            _ => FirstNonEmpty(identification.Title, identification.AlbumName, identification.SeriesTitle)
        };

        return (query, identification.ReleaseYear?.Year);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
