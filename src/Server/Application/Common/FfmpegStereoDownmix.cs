using System.Globalization;

namespace K7.Server.Application.Common;

/// <summary>
/// Explicit stereo downmix matrices for HLS AAC encode (ffmpeg <c>pan</c> filter).
/// <c>-ac 2</c> leaves the mix to libswresample defaults: the center (dialogue) is spread
/// at -3 dB and the whole mix is normalized, so voices sit low under effects, LFE is
/// mostly dropped, and unusual layouts can end up with near-silent channels.
/// One matrix per source layout, voices forward, LFE kept at a moderate level.
/// Unknown layouts fall back to <c>-ac 2</c>.
/// </summary>
public static class FfmpegStereoDownmix
{
    private const string Front51 = "FL=0.8*FC+0.6*FL+0.6*{L}+0.5*LFE|FR=0.8*FC+0.6*FR+0.6*{R}+0.5*LFE";
    private const string Front50 = "FL=0.8*FC+0.6*FL+0.6*{L}|FR=0.8*FC+0.6*FR+0.6*{R}";
    private const string Front71 = "FL=0.8*FC+0.6*FL+0.4*BL+0.4*SL+0.5*LFE|FR=0.8*FC+0.6*FR+0.4*BR+0.4*SR+0.5*LFE";
    private const string Front70 = "FL=0.8*FC+0.6*FL+0.4*BL+0.4*SL|FR=0.8*FC+0.6*FR+0.4*BR+0.4*SR";

    private static readonly Dictionary<string, string> Matrices = new(StringComparer.OrdinalIgnoreCase)
    {
        // ffmpeg / ffprobe channel_layout names.
        ["5.1"] = Front51.Replace("{L}", "BL").Replace("{R}", "BR"),
        ["5.1(side)"] = Front51.Replace("{L}", "SL").Replace("{R}", "SR"),
        ["5.0"] = Front50.Replace("{L}", "BL").Replace("{R}", "BR"),
        ["5.0(side)"] = Front50.Replace("{L}", "SL").Replace("{R}", "SR"),
        ["7.1"] = Front71,
        ["7.1(wide)"] = Front71.Replace("SL", "FLC").Replace("SR", "FRC"),
        ["7.1(wide-side)"] = Front71.Replace("BL", "FLC").Replace("BR", "FRC"),
        ["7.0"] = Front70,
        ["6.1"] = "FL=0.8*FC+0.6*FL+0.5*SL+0.4*BC+0.5*LFE|FR=0.8*FC+0.6*FR+0.5*SR+0.4*BC+0.5*LFE",
        ["4.0"] = "FL=0.8*FC+0.6*FL+0.5*BC|FR=0.8*FC+0.6*FR+0.5*BC",
        ["quad"] = "FL=0.7*FL+0.5*BL|FR=0.7*FR+0.5*BR",
        ["3.0"] = "FL=0.8*FC+0.6*FL|FR=0.8*FC+0.6*FR",
        ["2.1"] = "FL=0.8*FL+0.4*LFE|FR=0.8*FR+0.4*LFE"
    };

    /// <summary>
    /// Builds <c>pan=stereo|...</c> for a multichannel source. Null when the layout is
    /// unknown, already mono/stereo, or the requested output is not stereo.
    /// </summary>
    public static string? TryBuildPanFilter(string? sourceChannelLayout, int sourceChannels, int outputChannels)
    {
        if (outputChannels != 2 || sourceChannels <= 2)
            return null;

        if (string.IsNullOrWhiteSpace(sourceChannelLayout))
            return null;

        // ffprobe can print "5.1(side)" or "6 channels (FL+FR+FC+LFE+SL+SR)"; keep the first token.
        var layout = sourceChannelLayout.Trim();
        var space = layout.IndexOf(' ');
        if (space > 0)
            layout = layout[..space];

        return Matrices.TryGetValue(layout, out var matrix)
            ? "pan=stereo|" + matrix
            : null;
    }

    /// <summary>
    /// Output-side ffmpeg argument for the filter (quoted: the matrix contains <c>|</c>).
    /// </summary>
    public static string ToFilterArgument(string panFilter) =>
        string.Create(CultureInfo.InvariantCulture, $"-af \"{panFilter}\"");
}
