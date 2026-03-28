using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
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

    public static bool TryIdentifyMusicTrack(this IndexedFile indexedFile, Library library, IEnumerable<IndexedFile> similarIndexedFiles)
    {
        string? trackTitle = indexedFile.Name;
        int? trackNumber = null;

        // Extract track number from filename (e.g., "01 - Song Title" -> trackNumber=1, trackTitle="Song Title")
        if (StringParsingHelper.TryApplyRegexes(trackTitle, Regexes.TrackNumberExtractionRegexes, false, out var trackNumberResult))
        {
            if (int.TryParse(trackNumberResult?.Output, out int parsedTrackNumber))
            {
                trackNumber = parsedTrackNumber;
            }
            trackTitle = trackNumberResult?.TrimmedInput ?? trackTitle;
        }

        // Clean the track title from noise
        if (StringParsingHelper.TryApplyRegexes(trackTitle, Regexes.TitleCleaningRegexes, true, out var cleanedResult))
        {
            trackTitle = cleanedResult?.Output ?? trackTitle;
        }

        if (string.IsNullOrWhiteSpace(trackTitle))
        {
            return false;
        }

        // Album name = parent directory, Artist name = grandparent directory
        // Structure: Artist/Album/01 - Track.flac  OR  Album/01 - Track.flac
        var albumName = GetAlbumDirectory(indexedFile, library);

        // Extract year from album directory name if present
        DateOnly? releaseYear = null;
        if (!string.IsNullOrEmpty(albumName) &&
            StringParsingHelper.TryApplyRegexes(albumName, Regexes.YearExtractionRegexes, false, out var albumYearResult))
        {
            if (int.TryParse(albumYearResult?.Output, out int year))
            {
                releaseYear = new DateOnly(year, 1, 1);
            }
            albumName = albumYearResult?.TrimmedInput ?? albumName;
        }

        indexedFile.Identification = new MediaIdentification(trackTitle)
        {
            ReleaseYear = releaseYear,
            TrackNumber = trackNumber,
            AlbumName = albumName,
            ArtistName = albumName is not null ? GetGrandparentDirectory(indexedFile, library) : null
        };

        return true;
    }

    private static string? GetAlbumDirectory(IndexedFile indexedFile, Library library)
    {
        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (string.IsNullOrEmpty(directory)) return null;

        var normalizedDir = Path.GetFullPath(directory);
        var normalizedRoot = Path.GetFullPath(library.RootPath);
        if (string.Equals(normalizedDir, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return indexedFile.ParentDirectory;
    }

    private static string? GetGrandparentDirectory(IndexedFile indexedFile, Library library)
    {
        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (string.IsNullOrEmpty(directory)) return null;

        var grandparent = Path.GetDirectoryName(directory);
        if (string.IsNullOrEmpty(grandparent)) return null;

        // Don't return the library root as the grandparent
        var normalizedGrandparent = Path.GetFullPath(grandparent);
        var normalizedRoot = Path.GetFullPath(library.RootPath);
        if (string.Equals(normalizedGrandparent, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFileName(grandparent);
    }

    public static bool TryIdentifySerieEpisode(this IndexedFile indexedFile, Library library, IEnumerable<IndexedFile> similarIndexedFiles)
    {
        var fileName = indexedFile.Name;

        // Clean anime fansub tags from filename before parsing
        var cleanedFileName = CleanAnimeTags(fileName);

        // Try SxxExx pattern first (highest priority)
        var sxxExxMatch = Regexes.EpisodeSxxExx().Match(cleanedFileName);
        if (sxxExxMatch.Success)
        {
            var seasonNumber = int.Parse(sxxExxMatch.Groups["season"].Value);
            var episodeNumber = int.Parse(sxxExxMatch.Groups["episode"].Value);
            var seriesTitle = ExtractSeriesTitle(cleanedFileName, sxxExxMatch.Index, indexedFile, library);
            var releaseYear = ExtractReleaseYear(seriesTitle, indexedFile, library, out var cleanedSeriesTitle);

            if (string.IsNullOrWhiteSpace(cleanedSeriesTitle)) return false;

            if (sxxExxMatch.Groups["multiEp"].Success)
            {
                // Multi-episode detected — only take the first episode, as per plan
            }

            indexedFile.Identification = new MediaIdentification(cleanedSeriesTitle)
            {
                ReleaseYear = releaseYear,
                SeriesTitle = cleanedSeriesTitle,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber
            };
            return true;
        }

        // Try NxNN pattern (e.g. 1x01)
        var nxnnMatch = Regexes.EpisodeNxNN().Match(cleanedFileName);
        if (nxnnMatch.Success)
        {
            var seasonNumber = int.Parse(nxnnMatch.Groups["season"].Value);
            var episodeNumber = int.Parse(nxnnMatch.Groups["episode"].Value);
            var seriesTitle = ExtractSeriesTitle(cleanedFileName, nxnnMatch.Index, indexedFile, library);
            var releaseYear = ExtractReleaseYear(seriesTitle, indexedFile, library, out var cleanedSeriesTitle);

            if (string.IsNullOrWhiteSpace(cleanedSeriesTitle)) return false;

            indexedFile.Identification = new MediaIdentification(cleanedSeriesTitle)
            {
                ReleaseYear = releaseYear,
                SeriesTitle = cleanedSeriesTitle,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber
            };
            return true;
        }

        // Try absolute numbering (anime: "Show Name - 1001")
        var absoluteMatch = Regexes.EpisodeAbsolute().Match(cleanedFileName);
        if (absoluteMatch.Success)
        {
            var episodeStr = absoluteMatch.Groups["episode"].Value;
            var episodeNum = int.Parse(episodeStr);

            // Guard against false positives: resolution patterns
            if (Regexes.ResolutionPattern().IsMatch(cleanedFileName)) return false;

            // Guard: numbers in the range 1928-2500 are likely years, not episodes
            if (episodeNum is >= 1928 and <= 2500) return false;

            var seriesTitle = ExtractSeriesTitle(cleanedFileName, absoluteMatch.Index, indexedFile, library);
            var releaseYear = ExtractReleaseYear(seriesTitle, indexedFile, library, out var cleanedSeriesTitle);

            if (string.IsNullOrWhiteSpace(cleanedSeriesTitle)) return false;

            // Season from folder if available, otherwise null (to be resolved by provider)
            var seasonFromFolder = ExtractSeasonFromFolder(indexedFile, library);

            indexedFile.Identification = new MediaIdentification(cleanedSeriesTitle)
            {
                ReleaseYear = releaseYear,
                SeriesTitle = cleanedSeriesTitle,
                SeasonNumber = seasonFromFolder,
                AbsoluteNumber = episodeNum
            };
            return true;
        }

        return false;
    }

    private static string CleanAnimeTags(string fileName)
    {
        // Remove [SubGroup], [1080p], [CRC32], v2 etc.
        var cleaned = Regexes.AnimeTags().Replace(fileName, " ");
        // Convert dots/underscores to spaces (except file extension dots handled earlier)
        cleaned = cleaned.Replace('_', ' ');
        // Only replace dots that are between word characters (not leading/trailing)
        cleaned = Regex.Replace(cleaned, @"(?<=\w)\.(?=\w)", " ");
        // Collapse multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        return cleaned;
    }

    private static string ExtractSeriesTitle(string cleanedFileName, int patternIndex, IndexedFile indexedFile, Library library)
    {
        // Try to extract title from filename before the episode pattern
        if (patternIndex > 0)
        {
            var titlePart = cleanedFileName[..patternIndex].Trim().TrimEnd('-', '.', '_', ' ');
            if (!string.IsNullOrWhiteSpace(titlePart))
                return titlePart;
        }

        // Fallback: use directory structure
        // If there's a season folder, use grandparent; otherwise use parent
        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (string.IsNullOrEmpty(directory)) return string.Empty;

        var dirName = Path.GetFileName(directory);
        if (!string.IsNullOrEmpty(dirName) && Regexes.SeasonFolder().IsMatch(dirName))
        {
            // Current parent is a season folder, use grandparent as series title
            var grandparent = Path.GetDirectoryName(directory);
            if (!string.IsNullOrEmpty(grandparent))
            {
                var gpName = Path.GetFileName(grandparent);
                var normalizedGp = Path.GetFullPath(grandparent);
                var normalizedRoot = Path.GetFullPath(library.RootPath);
                if (!string.Equals(normalizedGp, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(gpName))
                {
                    return gpName;
                }
            }
        }

        // Use parent directory if it's not the library root
        if (!string.IsNullOrEmpty(dirName))
        {
            var normalizedDir = Path.GetFullPath(directory);
            var normalizedRoot = Path.GetFullPath(library.RootPath);
            if (!string.Equals(normalizedDir, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return dirName;
            }
        }

        return string.Empty;
    }

    private static int? ExtractSeasonFromFolder(IndexedFile indexedFile, Library library)
    {
        var directory = Path.GetDirectoryName(indexedFile.Path);
        if (string.IsNullOrEmpty(directory)) return null;

        var dirName = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(dirName)) return null;

        var match = Regexes.SeasonFolder().Match(dirName);
        if (!match.Success) return null;

        if (match.Groups["specials"].Success)
            return 0;

        var seasonGroup = match.Groups["season"].Success ? match.Groups["season"] : match.Groups["season2"];
        if (seasonGroup.Success && int.TryParse(seasonGroup.Value, out var season))
            return season;

        return null;
    }

    private static DateOnly? ExtractReleaseYear(string title, IndexedFile indexedFile, Library library, out string cleanedTitle)
    {
        cleanedTitle = title;
        DateOnly? releaseYear = null;

        // Try extracting year from the series title
        if (StringParsingHelper.TryApplyRegexes(title, Regexes.YearExtractionRegexes, false, out var yearResult))
        {
            if (int.TryParse(yearResult?.Output, out int year))
            {
                releaseYear = new DateOnly(year, 1, 1);
            }
            cleanedTitle = yearResult?.TrimmedInput ?? title;
        }

        // If no year from title, try from parent/grandparent directories
        if (!releaseYear.HasValue)
        {
            var directory = Path.GetDirectoryName(indexedFile.Path);
            if (!string.IsNullOrEmpty(directory))
            {
                var dirName = Path.GetFileName(directory);
                // Check if parent is a season folder -> check grandparent
                if (!string.IsNullOrEmpty(dirName) && Regexes.SeasonFolder().IsMatch(dirName))
                {
                    var grandparent = Path.GetDirectoryName(directory);
                    if (!string.IsNullOrEmpty(grandparent))
                    {
                        var gpName = Path.GetFileName(grandparent);
                        if (!string.IsNullOrEmpty(gpName)
                            && StringParsingHelper.TryApplyRegexes(gpName, Regexes.YearExtractionRegexes, false, out var gpYearResult)
                            && int.TryParse(gpYearResult?.Output, out int gpYear))
                        {
                            releaseYear = new DateOnly(gpYear, 1, 1);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(dirName)
                    && StringParsingHelper.TryApplyRegexes(dirName, Regexes.YearExtractionRegexes, false, out var dirYearResult)
                    && int.TryParse(dirYearResult?.Output, out int dirYear))
                {
                    releaseYear = new DateOnly(dirYear, 1, 1);
                }
            }
        }

        // Clean the title
        if (StringParsingHelper.TryApplyRegexes(cleanedTitle, Regexes.TitleCleaningRegexes, true, out var cleanedResult))
        {
            cleanedTitle = cleanedResult?.Output ?? cleanedTitle;
        }

        return releaseYear;
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
