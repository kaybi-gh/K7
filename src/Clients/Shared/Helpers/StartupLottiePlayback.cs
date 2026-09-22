namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Timing for the Android DecorView Skottie splash.
/// Sequence: static <c>k7_logo</c> -> Skottie blit loop + Blazor as soon as ready.
/// </summary>
public static class StartupLottiePlayback
{
    // splash.json fades in over the first 12 frames at 30fps (~400ms).
    public const int LogoHideAfterMs = 400;
    // splash.json is 90 frames @ 30fps = 3000ms (legacy / failsafe helpers).
    public const int ReadyFallbackMs = 3000;
    public const int NoAnimationReadyMs = 300;

    /// <summary>
    /// Legacy full-duration helper. Android startup signals Blazor immediately
    /// when the Skottie driver is ready.
    /// </summary>
    public static long ReadyDelayMs(long durationMs, bool hasAnimation)
    {
        if (!hasAnimation)
            return NoAnimationReadyMs;

        return durationMs > 0 ? durationMs : ReadyFallbackMs;
    }

    public static bool ShouldHideStaticLogo(bool hasAnimation, long elapsedMs) =>
        hasAnimation && elapsedMs >= LogoHideAfterMs;

    /// <summary>
    /// Failsafe after 6s. Skip only when the DecorView overlay is already driving startup.
    /// </summary>
    public static bool ShouldAssignStartPageOnTimeout(bool overlayShown, bool startPageAssigned) =>
        !overlayShown || !startPageAssigned;

    public static bool TryGetFitRect(
        int canvasWidth,
        int canvasHeight,
        float animationWidth,
        float animationHeight,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        if (animationWidth <= 0 || animationHeight <= 0)
        {
            left = 0;
            top = 0;
            right = canvasWidth;
            bottom = canvasHeight;
            return false;
        }

        var scale = Math.Min(canvasWidth / animationWidth, canvasHeight / animationHeight);
        var width = animationWidth * scale;
        var height = animationHeight * scale;
        left = (canvasWidth - width) / 2f;
        top = (canvasHeight - height) / 2f;
        right = left + width;
        bottom = top + height;
        return true;
    }
}
