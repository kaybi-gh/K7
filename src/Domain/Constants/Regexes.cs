using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace MediaServer.Application.Helpers;
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

    [GeneratedRegex(@"^\s*(?<trimmedInput>.+?)[ _\,\.\(\)\[\]\-](3d|sbs|tab|hsbs|htab|mvc|HDR|HDC|UHD|UltraHD|4k|ac3|dts|custom|dc|divx|divx5|dsr|dsrip|dutch|dvd|dvdrip|dvdscr|dvdscreener|screener|dvdivx|cam|fragment|fs|hdtv|hdrip|hdtvrip|internal|limited|multi|subs|ntsc|ogg|ogm|pal|pdtv|proper|repack|rerip|retail|cd[1-9]|r5|bd5|bd|se|svcd|swedish|german|read.nfo|nfofix|unrated|ws|telesync|ts|telecine|tc|brrip|bdrip|480p|480i|576p|576i|720p|720i|1080p|1080i|2160p|hrhd|hrhdtv|hddvd|bluray|blu-ray|x264|x265|h264|h265|xvid|xvidvd|xxx|www.www|AAC|DTS|\[.*\])([ _\,\.\(\)\[\]\-]|$)")]
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
}
