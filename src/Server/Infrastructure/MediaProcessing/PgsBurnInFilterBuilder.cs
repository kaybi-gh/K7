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
        // PGS often reports 0x0. Blu-ray compositions are authored on a 16:9 canvas
        // (typically 1920x1080). Scope/cropped video (e.g. 1920x818) must be padded into
        // that canvas - otherwise bottom-aligned PGS land in the player's letterbox.
        if (subtitleWidth <= 0 || subtitleHeight <= 0)
        {
            if (videoWidth > 0)
            {
                subtitleWidth = videoWidth;
                var canvasHeight = (int)Math.Round(videoWidth * 9.0 / 16.0);
                if ((canvasHeight & 1) != 0)
                    canvasHeight++;
                subtitleHeight = Math.Max(videoHeight, canvasHeight);
            }
            else
            {
                subtitleWidth = videoWidth;
                subtitleHeight = videoHeight;
            }
        }

        return (videoWidth, videoHeight, subtitleWidth, subtitleHeight);
    }

    public static string BuildFilterComplex(
        int subStreamIndex,
        int videoWidth,
        int videoHeight,
        int subtitleWidth,
        int subtitleHeight,
        int? scaleHeight = null,
        string? additionalVideoFilter = null,
        string? preVideoFilter = null)
    {
        (videoWidth, videoHeight, subtitleWidth, subtitleHeight) = ResolveDimensions(
            videoWidth,
            videoHeight,
            subtitleWidth,
            subtitleHeight);

        var videoDar = (double)videoWidth / videoHeight;
        var subtitleDar = (double)subtitleWidth / subtitleHeight;
        var needsCanvasPadding = subtitleHeight > videoHeight || subtitleWidth > videoWidth;
        var needsPostFilters = scaleHeight is not null || !string.IsNullOrWhiteSpace(additionalVideoFilter);
        var overlayLabel = needsPostFilters ? "burned" : "vout";
        var hasPreFilter = !string.IsNullOrWhiteSpace(preVideoFilter);
        var videoLabel = hasPreFilter ? "vpre" : "0:v:0";

        var prefix = hasPreFilter
            ? string.Format(CultureInfo.InvariantCulture, "[0:v:0]{0}[vpre];", preVideoFilter)
            : string.Empty;

        string overlayGraph;
        if (!needsCanvasPadding && Math.Abs(videoDar - subtitleDar) < DarTolerance)
        {
            overlayGraph = string.Format(
                CultureInfo.InvariantCulture,
                "[{0}][0:{1}]scale2ref=flags=lanczos[base][sub];" +
                "[base][sub]overlay=format=auto:eof_action=pass:repeatlast=0[{2}]",
                videoLabel,
                subStreamIndex,
                overlayLabel);
        }
        else
        {
            // PGS is often authored on a 16:9 canvas (e.g. 1920x1080) while cinema-scope video
            // is shorter (e.g. 1920x818). Pad the video into the subtitle canvas before overlay.
            overlayGraph = string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]pad={1}:{2}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1[base];" +
                "[0:{3}]scale={1}:{2}:flags=lanczos,format=yuva420p[sub];" +
                "[base][sub]overlay=0:0:format=auto:eof_action=pass:repeatlast=0[{4}]",
                videoLabel,
                subtitleWidth,
                subtitleHeight,
                subStreamIndex,
                overlayLabel);
        }

        overlayGraph = prefix + overlayGraph;

        if (!needsPostFilters)
        {
            return overlayGraph;
        }

        var postFilters = new List<string>();
        if (scaleHeight is int height)
        {
            // Match AutoScaleArgument used by the simple -vf quality ladder.
            postFilters.Add(string.Format(
                CultureInfo.InvariantCulture,
                "scale=trunc(oh*a/2)*2:{0}",
                height));
        }

        if (!string.IsNullOrWhiteSpace(additionalVideoFilter))
        {
            postFilters.Add(additionalVideoFilter);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0};[burned]{1}[vout]",
            overlayGraph,
            string.Join(",", postFilters));
    }
}
