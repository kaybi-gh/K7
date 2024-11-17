using System.Diagnostics.CodeAnalysis;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Extensions;
public static class IndexedFileExtensions
{
    public static bool TryIdentifyMovie(this IndexedFile indexedFile, [NotNullWhen(true)] out MediaIdentification? movieIdentification)
    {
        string? title = null;
        string? year = null;

        if (StringParsingHelper.TryApplyRegexes(indexedFile.Name, Regexes.YearExtractionRegexes, false, out var fileYearResult))
        {
            year = fileYearResult?.Output;
            title = fileYearResult?.TrimmedInput;
        }

        if (StringParsingHelper.TryApplyRegexes(fileYearResult?.TrimmedInput, Regexes.TitleCleaningRegexes, true, out var fileTitleResult))
        {
            title = fileTitleResult?.Output ?? fileYearResult?.TrimmedInput;
        }

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(indexedFile.ParentDirectory))
        {
            if (StringParsingHelper.TryApplyRegexes(indexedFile.ParentDirectory, Regexes.YearExtractionRegexes, false, out var parentDirectoryYearResult))
            {
                year = parentDirectoryYearResult?.Output;
                title = parentDirectoryYearResult?.TrimmedInput;
            }

            if (StringParsingHelper.TryApplyRegexes(parentDirectoryYearResult?.TrimmedInput, Regexes.TitleCleaningRegexes, true, out var parentDirectoryTitleResult))
            {
                title = parentDirectoryTitleResult?.Output ?? parentDirectoryYearResult?.TrimmedInput;
            }
        }

        if (!string.IsNullOrEmpty(title))
        {
            movieIdentification = new MediaIdentification(title);

            if (int.TryParse(year, out int releaseYear))
            {
                movieIdentification.ReleaseYear = new DateOnly(releaseYear, 1, 1);
            }
            return true;
        }
        movieIdentification = null;
        return false;
    }

    public static void TryIdentifyMusicTrack(this IndexedFile indexedFile, Library library, IEnumerable<IndexedFile> similarIndexedFiles)
    {
    }

    public static void TryIdentifySerieEpisode(this IndexedFile indexedFile, Library library, IEnumerable<IndexedFile> similarIndexedFiles)
    {
    }
}

// Movie:
// One file can represent one movie or a part of a movie
// Can have a parent directory or none
// Title must be in filename or parent directory
// Release year should be in filename or parent directory

// MusicTrack:
// One file must represent one track
// Can have one or two parent directories or none
// Artist name must be in filename or parent directory or parent parent directory
// Album name must be in filename or parent directory
// Track title can be in filename
// Album release year must be in filename or parent directory

// SerieEpisode:
// One file can represent one episode or a part of an episode or multiple episodes
// Can have one or two parent directories or none
// Serie title must be in filename or parent directory or parent parent directory
// Episode(s) number(s) must be in filename
// Season number must exist and can be in filename or parent directory
// Episode(s) title(s) can be in filename
// Release year should be in filename or parent directory or parent parent directory
