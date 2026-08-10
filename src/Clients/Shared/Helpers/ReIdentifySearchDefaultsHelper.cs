using System.Text.RegularExpressions;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Helpers;

public static partial class ReIdentifySearchDefaultsHelper
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

    /// <summary>
    /// Path shown in the re-identify dialog: movie/episode file, or series root folder when detectable.
    /// </summary>
    public static string? ResolveSourcePath(
        IEnumerable<IndexedFileDto>? indexedFiles,
        MediaType mediaType,
        Guid? preferredIndexedFileId = null)
    {
        if (indexedFiles is null)
            return null;

        var files = indexedFiles.ToList();
        if (files.Count == 0)
            return null;

        IndexedFileDto? file = null;
        if (preferredIndexedFileId.HasValue)
            file = files.FirstOrDefault(f => f.Id == preferredIndexedFileId.Value);

        file ??= files[0];
        if (string.IsNullOrWhiteSpace(file.Path))
            return null;

        // File-scoped re-identify always shows that file.
        if (preferredIndexedFileId.HasValue)
            return file.Path;

        if (mediaType == MediaType.Serie)
            return TryGetSerieRootPath(file.Path) ?? file.Path;

        return file.Path;
    }

    private static string? TryGetSerieRootPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            return null;

        var dirName = Path.GetFileName(directory);
        if (!string.IsNullOrEmpty(dirName) && SeasonFolder().IsMatch(dirName))
        {
            var seriesRoot = Path.GetDirectoryName(directory);
            return string.IsNullOrEmpty(seriesRoot) ? null : seriesRoot;
        }

        // Flat layout: Show/episode.mkv -> Show
        return directory;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    // Keep in sync with K7.Server.Application.Helpers.Regexes.SeasonFolder
    [GeneratedRegex(@"^(?:Season|Saison|Series)\s*(?<season>\d{1,2})$|^S(?<season2>\d{1,2})$|^(?<specials>Specials?|Extras?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SeasonFolder();
}
