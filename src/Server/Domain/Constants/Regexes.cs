using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Helpers;

public static partial class Regexes
{
    #region Year extraction

    [GeneratedRegex(@"(?<trimmedInput>.+[^_\,\.\(\)\[\]\-])[_\.\(\)\[\]\-](?<output>19[0-9]{2}|20[0-9]{2})(?![0-9]+|\W[0-9]{2}\W[0-9]{2})([ _\,\.\(\)\[\]\-][^0-9]|).*(19[0-9]{2}|20[0-9]{2})*")]
    private static partial Regex Year();

    [GeneratedRegex(@"(?<trimmedInput>.+[^_\,\.\(\)\[\]\-])[ _\.\(\)\[\]\-]+(?<output>19[0-9]{2}|20[0-9]{2})(?![0-9]+|\W[0-9]{2}\W[0-9]{2})([ _\,\.\(\)\[\]\-][^0-9]|).*(19[0-9]{2}|20[0-9]{2})*")]
    private static partial Regex Year_WithSpaces();

    public static readonly FrozenSet<Regex> YearExtractionRegexes = new List<Regex> {
        Year(),
        Year_WithSpaces()
    }.ToFrozenSet();

    #endregion

    #region Title cleaning

    [GeneratedRegex(@"^\s*(?<trimmedInput>.+?)[ _\,\.\(\)\[\]\-](3d|sbs|tab|hsbs|htab|mvc|HDR|HDC|UHD|UltraHD|4k|ac3|dts|custom|dc|divx|divx5|dsr|dsrip|dutch|dvd|dvdrip|dvdscr|dvdscreener|screener|dvdivx|cam|fragment|fs|hdtv|hdrip|hdtvrip|internal|limited|multi|subs|ntsc|ogg|ogm|pal|pdtv|proper|repack|rerip|retail|cd[1-9]|r5|bd5|bd|se|svcd|swedish|german|truefrench|vostfr|vff|vfq|read.nfo|nfofix|unrated|ws|telesync|ts|telecine|tc|brrip|bdrip|480p|480i|576p|576i|720p|720i|1080p|1080i|2160p|hrhd|hrhdtv|hddvd|bluray|blu-ray|x264|x265|h264|h265|xvid|xvidvd|xxx|www.www|AAC|DTS|\[.*\])([ _\,\.\(\)\[\]\-]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Title_RemoveInformations();

    [GeneratedRegex(@"^(?<trimmedInput>.+?)(\[.*\])")]
    private static partial Regex Title_RemoveInformationsBetweenBrackets_1();

    [GeneratedRegex(@"^\s*(?<trimmedInput>.+?)\WE[0-9]+(-|~)E?[0-9]+(\W|$)")]
    private static partial Regex Title_RemoveEpisodesInformations();

    [GeneratedRegex(@"^\s*\[[^\]]+\](?!\.\w+$)\s*(?<trimmedInput>.+)")]
    private static partial Regex Title_RemoveInformationsBetweenBrackets_2();

    [GeneratedRegex(@"^\s*(?<trimmedInput>.+?)\s+-\s+[0-9]+\s*$")]
    private static partial Regex Title_RemoveEndNumber();

    [GeneratedRegex(@"^\s*(?<trimmedInput>.+?)(([-._ ](trailer|sample))|-(scene|clip|behindthescenes|deleted|deletedscene|featurette|short|interview|other|extra))$")]
    private static partial Regex Title_RemoveVideoContentType();

    public static readonly FrozenSet<Regex> TitleCleaningRegexes = new List<Regex> {
        Title_RemoveInformations(),
        Title_RemoveInformationsBetweenBrackets_1(),
        Title_RemoveEpisodesInformations(),
        Title_RemoveInformationsBetweenBrackets_2(),
        Title_RemoveEndNumber(),
        Title_RemoveVideoContentType()
    }.ToFrozenSet();

    #endregion

    #region Music track number extraction

    // Matches: "01 - Song Title", "01. Song Title", "1 Song Title", "01-Song Title"
    [GeneratedRegex(@"^\s*(?<output>\d{1,3})[\s.\-_]+(?<trimmedInput>.+)$")]
    private static partial Regex TrackNumber_LeadingNumber();

    public static readonly FrozenSet<Regex> TrackNumberExtractionRegexes = new List<Regex> {
        TrackNumber_LeadingNumber()
    }.ToFrozenSet();

    #endregion

    #region Episode extraction

    // S01E01, S01E01-E03, S01E01E02, S1E1
    [GeneratedRegex(@"[Ss](?<season>\d{1,2})\s*[Ee](?<episode>\d{1,4})(?:[\-Ee]+(?<multiEp>\d{1,4}))*", RegexOptions.Compiled)]
    public static partial Regex EpisodeSxxExx();

    // 1x01, 1x01-03
    [GeneratedRegex(@"(?<!\d)(?<season>\d{1,2})[Xx](?<episode>\d{1,3})(?:\-(?<multiEp>\d{1,3}))*", RegexOptions.Compiled)]
    public static partial Regex EpisodeNxNN();

    // Absolute numbering: "Show Name - 1001" (anime-style, 2-4 digit episode number at end)
    [GeneratedRegex(@"(?:^|[\s\-._])(?<episode>\d{2,4})(?:\s*v\d)?(?:[\s\-._]|$)", RegexOptions.Compiled)]
    public static partial Regex EpisodeAbsolute();

    // "01 - Title", "05. Episode", "1-Title" (only used when a season folder is already known)
    [GeneratedRegex(@"^\s*(?<episode>\d{1,3})[\s.\-_]+.+$", RegexOptions.Compiled)]
    public static partial Regex EpisodeLeadingNumber();

    // Season from folder name: "Season 1", "Saison 2", "S01", "Show Name S04",
    // "Show - Saison 01 - DVDRip TrueFrench - Group", "Specials"
    [GeneratedRegex(@"^(?:Season|Saison|Series)\s*(?<season>\d{1,2})$|^S(?<season2>\d{1,2})$|^(?<specials>Specials?|Extras?)$|(?:Season|Saison|Series|S)(?<season3>\d{1,2})$|\b(?:Season|Saison)\s*(?<season4>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    public static partial Regex SeasonFolder();

    [GeneratedRegex(@"\b(?:Season|Saison)\s*\d{1,2}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SeasonFolderMarker();

    public static bool IsSeasonFolder(string folderName) =>
        !string.IsNullOrEmpty(folderName) && SeasonFolder().IsMatch(folderName);

    public static bool TryParseSeasonFolder(string folderName, out int seasonNumber)
    {
        seasonNumber = 0;
        if (string.IsNullOrEmpty(folderName))
            return false;

        var match = SeasonFolder().Match(folderName);
        if (!match.Success)
            return false;

        if (match.Groups["specials"].Success)
            return true;

        foreach (var groupName in new[] { "season", "season2", "season3", "season4" })
        {
            var group = match.Groups[groupName];
            if (group.Success && int.TryParse(group.Value, out seasonNumber))
                return true;
        }

        return false;
    }

    public static bool TryParseLeadingEpisodeNumber(string fileName, out int episodeNumber)
    {
        episodeNumber = 0;
        if (string.IsNullOrEmpty(fileName))
            return false;

        var match = EpisodeLeadingNumber().Match(fileName);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups["episode"].Value, out episodeNumber) && episodeNumber is >= 1 and <= 999;
    }

    /// <summary>
    /// "Warehouse 13 - Saison 01 - DVDRip ..." -> "Warehouse 13". Exact "Saison 5" folders return null.
    /// </summary>
    public static string? StripSeasonFolderDecorations(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return null;

        var match = SeasonFolderMarker().Match(folderName);
        if (!match.Success)
            return folderName.Trim();

        var before = folderName[..match.Index].Trim().TrimEnd('-', '.', '_', ' ');
        return string.IsNullOrWhiteSpace(before) ? null : before;
    }

    // Clean anime fansub tags: [SubGroup], [1080p], [AABBCCDD] (CRC32), v2/v3
    [GeneratedRegex(@"\[[^\]]+\]|(?<=\s)v\d(?:\s|$)", RegexOptions.Compiled)]
    public static partial Regex AnimeTags();

    // False positive guards: resolution patterns that look like episode numbers
    [GeneratedRegex(@"(?:^|\D)(?:1920x1080|1280x720|3840x2160|1080[pi]|720[pi]|2160[pi]|480[pi]|576[pi]|4[Kk])(?:\D|$)", RegexOptions.Compiled)]
    public static partial Regex ResolutionPattern();

    #endregion
}
