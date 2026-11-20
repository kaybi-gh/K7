namespace K7.Server.Application.Helpers;

/// <summary>
/// Finds end credits from the picture alone: a run of "cards" (lettering on a flat background,
/// black or not) that reaches the end of the file. This is the only signal left when every
/// episode closes on different music, where audio fingerprints share nothing.
/// </summary>
/// <remarks>
/// Each sampled frame is reduced to three numbers measured against its most common colour:
/// the share of pixels close to it (background), the share far from it (ink) and what is left
/// (mid-tones). Credits are mostly background with a little ink. A dark story scene is mostly
/// background too, but its other pixels are dim mid-tones rather than lettering, which is what
/// tells the two apart.
/// </remarks>
public static class CreditsFrameAnalyzer
{
    /// <summary>Frames are sampled once per second at this size, as packed RGB24.</summary>
    public const int FrameWidth = 160;
    public const int FrameHeight = 90;
    public const int FrameBytes = FrameWidth * FrameHeight * 3;
    public const double SampleIntervalSeconds = 1.0;

    /// <summary>Tail of the episode that is scanned.</summary>
    public const double TailSeconds = 450;
    public const double TailMaxShare = 0.5;

    public const double MinCreditsDurationSeconds = 15;

    /// <summary>Credits found by picture alone must stop this close to the end of the file.</summary>
    public const double MaxDistanceFromEndSeconds = 60;

    /// <summary>A run that stops this close to the end is extended to it (trailing studio logos).</summary>
    public const double SnapToEndSeconds = 10;

    /// <summary>
    /// The tail is decoded backwards from the end of the file in chunks of this length, and
    /// only for as long as <see cref="MayStartEarlier"/> holds: most episodes have credits
    /// shorter than one chunk, or none.
    /// </summary>
    public const double ChunkSeconds = 90;

    private const int BackgroundDistance = 28;
    private const int InkDistance = 70;
    private const double MinBackgroundShare = 0.78;
    private const double MaxInkShare = 0.2;
    private const double MinInkShare = 0.002;
    private const double MinBlackCardContentShare = 0.004;
    private const double BlankBackgroundShare = 0.97;
    private const int TrueBlackMaxChannel = 3;
    private const double DarkBackgroundLuma = 40;
    private const double DarkMidToInkRatio = 1.25;
    private const double DarkMidAllowance = 0.004;
    private const int MaxGapFrames = 5;
    private const double MinLetteredShare = 0.5;

    public readonly record struct FrameStats(
        double BackgroundShare,
        double InkShare,
        double BackgroundRed,
        double BackgroundGreen,
        double BackgroundBlue);

    public readonly record struct CreditsRun(double StartSeconds, double EndSeconds);

    public static double GetTailSeconds(double durationSeconds) =>
        Math.Min(TailSeconds, durationSeconds * TailMaxShare);

    /// <summary>
    /// Makes a sampled span hold exactly one frame per <see cref="SampleIntervalSeconds"/>, so
    /// that spans sampled separately line up when joined. A span that came out short is filled
    /// with its last frame.
    /// </summary>
    /// <param name="frames">Frames in order, one every <see cref="SampleIntervalSeconds"/>.</param>
    /// <param name="sampledSeconds">Length of the span that was sampled.</param>
    public static IReadOnlyList<FrameStats> FitToSpan(IReadOnlyList<FrameStats> frames, double sampledSeconds)
    {
        var expected = (int)(sampledSeconds / SampleIntervalSeconds);
        if (frames.Count == expected)
            return frames;

        var fitted = new List<FrameStats>(expected);
        for (var i = 0; i < expected; i++)
            fitted.Add(frames.Count == 0 ? default : frames[Math.Min(i, frames.Count - 1)]);

        return fitted;
    }

    /// <summary>
    /// True when credits found in these frames could begin before the first one, so that
    /// earlier frames are needed. Once the frames open on more footage than a run can bridge,
    /// nothing before them can belong to credits that reach the end of the file.
    /// </summary>
    /// <param name="frames">Frames in order, ending at the end of the file.</param>
    public static bool MayStartEarlier(IReadOnlyList<FrameStats> frames)
    {
        var lead = Math.Min(frames.Count, MaxGapFrames + 1);
        for (var i = 0; i < lead; i++)
        {
            if (IsLetteredCard(frames[i]) || frames[i].BackgroundShare >= BlankBackgroundShare)
                return true;
        }

        return false;
    }

    /// <summary>Measures one <see cref="FrameWidth"/> x <see cref="FrameHeight"/> RGB24 frame.</summary>
    public static FrameStats ComputeStats(ReadOnlySpan<byte> rgb)
    {
        var pixels = rgb.Length / 3;
        if (pixels == 0)
            return default;

        // Most common colour, on a coarse 8x8x8 grid.
        Span<int> counts = stackalloc int[512];
        Span<long> sums = stackalloc long[512 * 3];
        counts.Clear();
        sums.Clear();

        for (var i = 0; i < pixels; i++)
        {
            var r = rgb[i * 3];
            var g = rgb[i * 3 + 1];
            var b = rgb[i * 3 + 2];
            var bin = ((r >> 5) << 6) | ((g >> 5) << 3) | (b >> 5);
            counts[bin]++;
            sums[bin * 3] += r;
            sums[bin * 3 + 1] += g;
            sums[bin * 3 + 2] += b;
        }

        var mode = 0;
        for (var bin = 1; bin < 512; bin++)
        {
            if (counts[bin] > counts[mode])
                mode = bin;
        }

        var bgR = (double)sums[mode * 3] / counts[mode];
        var bgG = (double)sums[mode * 3 + 1] / counts[mode];
        var bgB = (double)sums[mode * 3 + 2] / counts[mode];

        var background = 0;
        var ink = 0;
        for (var i = 0; i < pixels; i++)
        {
            var distance = Math.Max(
                Math.Abs(rgb[i * 3] - bgR),
                Math.Max(Math.Abs(rgb[i * 3 + 1] - bgG), Math.Abs(rgb[i * 3 + 2] - bgB)));
            if (distance <= BackgroundDistance)
                background++;
            else if (distance >= InkDistance)
                ink++;
        }

        return new FrameStats((double)background / pixels, (double)ink / pixels, bgR, bgG, bgB);
    }

    /// <summary>
    /// Longest run of credit cards that reaches the end of the file, or null.
    /// </summary>
    /// <param name="frames">Frames in order, one every <see cref="SampleIntervalSeconds"/>.</param>
    /// <param name="firstFrameSeconds">Position of the first frame in the file.</param>
    /// <param name="durationSeconds">Duration of the file.</param>
    public static CreditsRun? FindCreditsRun(
        IReadOnlyList<FrameStats> frames,
        double firstFrameSeconds,
        double durationSeconds)
    {
        var count = frames.Count;
        if (count == 0 || durationSeconds <= 0)
            return null;

        var lettered = new bool[count];
        var blank = new bool[count];
        for (var i = 0; i < count; i++)
        {
            lettered[i] = IsLetteredCard(frames[i]);
            blank[i] = !lettered[i] && frames[i].BackgroundShare >= BlankBackgroundShare;
        }

        (int Start, int End)? best = null;
        var index = 0;
        while (index < count)
        {
            if (!lettered[index])
            {
                index++;
                continue;
            }

            // A run starts on lettering, continues through blank frames between cards, and
            // tolerates a few seconds of anything else (a logo, a photo behind the names).
            var cursor = index;
            var last = index;
            while (cursor + 1 < count
                && (lettered[cursor + 1] || blank[cursor + 1] || cursor + 1 - last <= MaxGapFrames))
            {
                cursor++;
                if (lettered[cursor] || blank[cursor])
                    last = cursor;
            }

            if (IsCredits(lettered, index, last, firstFrameSeconds, durationSeconds)
                && (best is null || last - index > best.Value.End - best.Value.Start))
            {
                best = (index, last);
            }

            index = last + 1;
        }

        if (best is null)
            return null;

        var start = firstFrameSeconds + best.Value.Start * SampleIntervalSeconds;
        var end = firstFrameSeconds + (best.Value.End + 1) * SampleIntervalSeconds;
        if (durationSeconds - end <= SnapToEndSeconds)
            end = durationSeconds;

        return new CreditsRun(start, Math.Min(end, durationSeconds));
    }

    private static bool IsCredits(
        bool[] lettered,
        int start,
        int end,
        double firstFrameSeconds,
        double durationSeconds)
    {
        var frames = end - start + 1;
        if (frames * SampleIntervalSeconds < MinCreditsDurationSeconds)
            return false;

        var letteredFrames = 0;
        for (var i = start; i <= end; i++)
        {
            if (lettered[i])
                letteredFrames++;
        }

        if ((double)letteredFrames / frames < MinLetteredShare)
            return false;

        var endSeconds = firstFrameSeconds + (end + 1) * SampleIntervalSeconds;
        return durationSeconds - endSeconds <= MaxDistanceFromEndSeconds;
    }

    private static bool IsLetteredCard(FrameStats frame)
    {
        if (frame.BackgroundShare < MinBackgroundShare || frame.InkShare > MaxInkShare)
            return false;

        var mid = Math.Max(0, 1 - frame.BackgroundShare - frame.InkShare);
        var maxChannel = Math.Max(frame.BackgroundRed, Math.Max(frame.BackgroundGreen, frame.BackgroundBlue));

        // Pure black never happens in a graded scene: any content on it is lettering, even
        // small print that the downscale has faded to mid-tones.
        if (maxChannel <= TrueBlackMaxChannel)
            return frame.InkShare + mid >= MinBlackCardContentShare;

        var luma = 0.299 * frame.BackgroundRed + 0.587 * frame.BackgroundGreen + 0.114 * frame.BackgroundBlue;
        if (luma < DarkBackgroundLuma)
        {
            // Night scenes are mostly dark too, but dim: lettering is ink with thin edges.
            return frame.InkShare >= MinInkShare
                && mid <= frame.InkShare * DarkMidToInkRatio + DarkMidAllowance;
        }

        return frame.InkShare >= MinInkShare;
    }
}
