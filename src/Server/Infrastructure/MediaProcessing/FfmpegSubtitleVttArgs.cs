using System.Globalization;
using System.Text;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// ffmpeg command line that converts text subtitle streams to WebVTT.
/// </summary>
internal static class FfmpegSubtitleVttArgs
{
    /// <summary>
    /// One input, one WebVTT output per stream (a single read of the file whatever their
    /// number). The format is forced, so an output path may be a temporary name.
    /// </summary>
    /// <param name="inputFilePath">Media file.</param>
    /// <param name="outputs">Stream index and output path of each track.</param>
    /// <param name="window">
    /// When set, only the cues between the two times are written. ffmpeg input-seeks to the
    /// start through the container index instead of reading the file from the beginning.
    /// </param>
    public static string Build(
        string inputFilePath,
        IReadOnlyList<KeyValuePair<int, string>> outputs,
        (TimeSpan Start, TimeSpan End)? window = null)
    {
        var args = new StringBuilder("-hide_banner -loglevel error -nostdin -y");

        if (window is { } seek)
            args.Append(" -ss ").Append(Seconds(seek.Start));

        args.Append(" -i \"").Append(inputFilePath).Append('"');

        // -copyts -start_at_zero keep the cue times on the media timeline, identical to those
        // of a full extraction whatever the container start time is: a window must never
        // shift subtitles against the picture.
        if (window is not null)
            args.Append(" -copyts -start_at_zero");

        foreach (var (streamIndex, outputPath) in outputs)
        {
            args.Append(" -map 0:").Append(streamIndex.ToString(CultureInfo.InvariantCulture)).Append(" -c:s webvtt");

            // The end is an output option on purpose: an input -t is not applied to
            // subtitles being re-encoded and ffmpeg would read on to the end of the file.
            if (window is { } range)
                args.Append(" -to ").Append(Seconds(range.End));

            args.Append(" -f webvtt \"").Append(outputPath).Append('"');
        }

        return args.ToString();
    }

    private static string Seconds(TimeSpan time) =>
        time.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
