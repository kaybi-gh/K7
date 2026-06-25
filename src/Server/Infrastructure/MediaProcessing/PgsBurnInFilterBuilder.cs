using System.Globalization;

namespace K7.Server.Infrastructure.MediaProcessing;

internal static class PgsBurnInFilterBuilder
{
    private const double DarTolerance = 0.01;

    public static string? BuildCanvasSizeArgument(int subtitleWidth, int subtitleHeight)
    {
        if (subtitleWidth <= 0 || subtitleHeight <= 0)
        {
            return null;
        }

        return string.Format(CultureInfo.InvariantCulture, "-canvas_size {0}x{1}", subtitleWidth, subtitleHeight);
    }

    public static (int VideoWidth, int VideoHeight, int SubtitleWidth, int SubtitleHeight) ResolveDimensions(
        int videoWidth,
        int videoHeight,
        int subtitleWidth,
        int subtitleHeight)
    {
        if (subtitleWidth <= 0 || subtitleHeight <= 0)
        {
            (subtitleWidth, subtitleHeight) = InferPgsCanvasSize(videoWidth, videoHeight);
        }

        return (videoWidth, videoHeight, subtitleWidth, subtitleHeight);
    }

    public static string BuildFilterComplex(
        int subStreamIndex,
        int videoWidth,
        int videoHeight,
        int subtitleWidth,
        int subtitleHeight)
    {
        (videoWidth, videoHeight, subtitleWidth, subtitleHeight) = ResolveDimensions(
            videoWidth,
            videoHeight,
            subtitleWidth,
            subtitleHeight);

        var videoDar = (double)videoWidth / videoHeight;
        var subtitleDar = (double)subtitleWidth / subtitleHeight;
        var needsCanvasPadding = subtitleHeight > videoHeight || subtitleWidth > videoWidth;

        if (!needsCanvasPadding && Math.Abs(videoDar - subtitleDar) < DarTolerance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[0:v:0][0:{0}]scale2ref=flags=lanczos[base][sub];" +
                "[base][sub]overlay=format=auto:eof_action=pass:repeatlast=0[vout]",
                subStreamIndex);
        }

        // PGS is often authored on a 16:9 canvas (e.g. 1920x1080) while cinema-scope video
        // is shorter (e.g. 1920x818). Pad the video into the subtitle canvas before overlay.
        return string.Format(
            CultureInfo.InvariantCulture,
            "[0:v:0]pad={0}:{1}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1[base];" +
            "[0:{2}]scale={0}:{1}:flags=lanczos,format=yuva420p[sub];" +
            "[base][sub]overlay=0:0:format=auto:eof_action=pass:repeatlast=0[vout]",
            subtitleWidth,
            subtitleHeight,
            subStreamIndex);
    }

    private static (int Width, int Height) InferPgsCanvasSize(int videoWidth, int videoHeight)
    {
        if (videoWidth >= 3840)
        {
            return (3840, 2160);
        }

        if (videoWidth >= 1920)
        {
            return (1920, 1080);
        }

        if (videoWidth >= 1280)
        {
            return (1280, 720);
        }

        return (720, 576);
    }
}
