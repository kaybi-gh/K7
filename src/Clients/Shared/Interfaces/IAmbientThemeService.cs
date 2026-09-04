namespace K7.Clients.Shared.Interfaces;

public interface IAmbientThemeService
{
    Guid? CurrentMediaId { get; }

    /// <summary>
    /// True when the current media theme reached natural end or was interrupted (watch / trailer)
    /// and must not auto-restart until the theme context is cleared or a different media starts.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Starts or keeps theme playback for <paramref name="mediaId"/>. Same media is a no-op
    /// (including after natural end or watch/trailer interrupt). A different media crossfades
    /// when something is already playing.
    /// </summary>
    Task KeepOrStartAsync(
        Guid mediaId,
        string themeUrl,
        byte[] audioBytes,
        double volume = 0.25,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a fade-out after a short grace period so navigation within the same media
    /// tree (serie -> season -> episode) can cancel the leave and keep playing.
    /// </summary>
    void ScheduleLeave(Guid mediaId);

    /// <summary>
    /// Fades out a watch/trailer interrupt while keeping the media context as finished so
    /// returning to the same series/movie tree does not restart the theme.
    /// </summary>
    Task InterruptAsync(Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fades out and clears the theme context (leaving the media tree, or no theme available).
    /// </summary>
    Task FadeOutAsync(double durationSeconds = 0.5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops playback immediately without fading.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
