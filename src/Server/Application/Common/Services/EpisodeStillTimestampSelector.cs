namespace K7.Server.Application.Common.Services;

public static class EpisodeStillTimestampSelector
{
    private const double IntroBufferSeconds = 30;
    private const double ContentStartRatio = 0.05;
    private const double ContentEndRatio = 0.90;
    private const double TargetRatioInWindow = 0.25;
    private const double BlackFrameToleranceSeconds = 2;

    public static double SelectTimestamp(
        double durationSeconds,
        double? introEndSeconds,
        IReadOnlyList<double> keyframeTimestamps,
        IReadOnlyList<double> blackFrameTimestamps)
    {
        var (windowStart, windowEnd) = GetContentWindow(durationSeconds, introEndSeconds);
        var target = windowStart + (windowEnd - windowStart) * TargetRatioInWindow;

        var candidates = keyframeTimestamps
            .Where(timestamp => timestamp >= windowStart && timestamp <= windowEnd)
            .Where(timestamp => !IsNearBlackFrame(timestamp, blackFrameTimestamps))
            .ToList();

        if (candidates.Count == 0)
        {
            if (!IsNearBlackFrame(target, blackFrameTimestamps))
                return Math.Clamp(target, 0, Math.Max(durationSeconds - 1, 0));

            return windowStart + (windowEnd - windowStart) * 0.5;
        }

        return candidates.MinBy(timestamp => Math.Abs(timestamp - target));
    }

    public static (double WindowStart, double WindowEnd) GetContentWindow(double durationSeconds, double? introEndSeconds)
    {
        var windowStart = introEndSeconds is not null
            ? Math.Min(introEndSeconds.Value + IntroBufferSeconds, durationSeconds * ContentEndRatio)
            : durationSeconds * ContentStartRatio;

        var windowEnd = durationSeconds * ContentEndRatio;

        if (windowEnd <= windowStart + 1)
        {
            windowStart = durationSeconds * ContentStartRatio;
            windowEnd = Math.Max(windowEnd, windowStart + 1);
        }

        return (windowStart, windowEnd);
    }

    private static bool IsNearBlackFrame(double timestamp, IReadOnlyList<double> blackFrameTimestamps) =>
        blackFrameTimestamps.Any(blackFrame => Math.Abs(blackFrame - timestamp) <= BlackFrameToleranceSeconds);
}
