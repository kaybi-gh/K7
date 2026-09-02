namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Scores HDMI display modes against content fps:
/// HD only, no resolution below the file, qualify 1x / 2x / 2.5x, prefer 1x
/// over 2.5x even at another HD size. SCALE_ON_DEVICE keeps the current panel
/// size when the rate rank is equal. SCALE_ON_TV (preferContentResolution)
/// picks the HDMI size closest to the file. 23.976 on 60 Hz is 3:2 pulldown.
/// </summary>
public static class AndroidHdmiFrameRateMatching
{
    public const float HarmonicToleranceHz = 0.05f;
    private const double DoubleRatePenalty = 1.0;
    private const double PulldownPenalty = 2.0;
    private const double OtherResolutionPenalty = 0.5;
    private const double UnqualifiedScore = 100;

    public static bool IsHdMode(int width, int height) =>
        width >= 1280 && height >= 720;

    public static bool AllowsModeResolution(
        int modeWidth,
        int modeHeight,
        int contentWidth,
        int contentHeight)
    {
        if (!IsHdMode(modeWidth, modeHeight))
            return false;
        if (contentWidth > 0 && modeWidth < contentWidth)
            return false;
        if (contentHeight > 0 && modeHeight < contentHeight)
            return false;
        return true;
    }

    public static bool QualifiesRefreshRate(float hz, float fps)
    {
        if (fps <= 1f || hz <= 1f)
            return false;

        return Matches(hz, fps)
            || Matches(hz, fps * 2f)
            || Matches(hz, fps * 2.5f);
    }

    public static double RateScore(float hz, float fps)
    {
        if (!QualifiesRefreshRate(hz, fps))
            return UnqualifiedScore;

        var direct = Math.Abs(hz - fps);
        if (direct <= HarmonicToleranceHz)
            return direct;

        var doubleRate = HarmonicScore(hz, fps * 2f, DoubleRatePenalty);
        var pulldown = HarmonicScore(hz, fps * 2.5f, PulldownPenalty);
        return Math.Min(doubleRate, pulldown);
    }

    private static double HarmonicScore(float hz, float target, double penalty)
    {
        var delta = Math.Abs(hz - target);
        return delta <= HarmonicToleranceHz ? delta + penalty : UnqualifiedScore;
    }

    private static bool Matches(float hz, float target) =>
        Math.Abs(hz - target) <= HarmonicToleranceHz;

    public static double ModeScore(
        float modeHz,
        int modeWidth,
        int modeHeight,
        float fps,
        int currentWidth,
        int currentHeight,
        int contentWidth = 0,
        int contentHeight = 0,
        bool preferContentResolution = false)
    {
        if (!AllowsModeResolution(modeWidth, modeHeight, contentWidth, contentHeight))
            return double.MaxValue;
        if (!QualifiesRefreshRate(modeHz, fps))
            return double.MaxValue;

        var rate = RateScore(modeHz, fps);
        if (preferContentResolution && (contentWidth > 0 || contentHeight > 0))
            return rate + ContentResolutionPenalty(modeWidth, modeHeight, contentWidth, contentHeight);

        var sameRes = modeWidth == currentWidth && modeHeight == currentHeight;
        return sameRes ? rate : rate + OtherResolutionPenalty;
    }

    /// <summary>
    /// Scale on TV: among 1x/2x/2.5x modes, pick the HDMI size closest
    /// to the file. 0.5 at 4K vs 1080p stays below pulldown (2.0) so 4K 1x still
    /// beats 1080p 59.94 when no 1080p 23.98 exists.
    /// </summary>
    public static double ContentResolutionPenalty(
        int modeWidth,
        int modeHeight,
        int contentWidth,
        int contentHeight)
    {
        var widthDelta = contentWidth > 0 ? Math.Abs(modeWidth - contentWidth) / 1920d : 0;
        var heightDelta = contentHeight > 0 ? Math.Abs(modeHeight - contentHeight) / 1080d : 0;
        return Math.Max(widthDelta, heightDelta) * OtherResolutionPenalty;
    }

    /// <summary>
    /// No hysteresis: switch when another qualified mode scores better.
    /// </summary>
    public static bool ShouldSwitch(double currentScore, double bestScore) =>
        bestScore < currentScore;

    public static HdmiCadenceKind ClassifyCadence(float contentFps, float displayHz)
    {
        if (contentFps <= 1f || displayHz <= 1f)
            return HdmiCadenceKind.Unknown;

        if (Math.Abs(displayHz - contentFps) <= HarmonicToleranceHz)
            return HdmiCadenceKind.Match1x;
        if (Math.Abs(displayHz - contentFps * 2f) <= HarmonicToleranceHz)
            return HdmiCadenceKind.Match2x;
        if (Math.Abs(displayHz - contentFps * 2.5f) <= HarmonicToleranceHz)
            return HdmiCadenceKind.Match25x;
        if (IsFilmFps(contentFps) && Math.Abs(displayHz - 60f) <= 0.2f)
            return HdmiCadenceKind.Pulldown32;

        return HdmiCadenceKind.Mismatch;
    }

    public static string DescribeCadence(HdmiCadenceKind kind) => kind switch
    {
        HdmiCadenceKind.Match1x => "1x",
        HdmiCadenceKind.Match2x => "2x",
        HdmiCadenceKind.Match25x => "2.5x",
        HdmiCadenceKind.Pulldown32 => "3:2 pulldown",
        HdmiCadenceKind.Mismatch => "mismatch",
        _ => "unknown"
    };

    public static bool IsCadenceWarning(HdmiCadenceKind kind) =>
        kind is HdmiCadenceKind.Pulldown32 or HdmiCadenceKind.Mismatch;

    private static bool IsFilmFps(float fps) => fps is >= 23.5f and <= 24.5f;
}

public enum HdmiCadenceKind
{
    Unknown,
    Match1x,
    Match2x,
    Match25x,
    Pulldown32,
    Mismatch
}
