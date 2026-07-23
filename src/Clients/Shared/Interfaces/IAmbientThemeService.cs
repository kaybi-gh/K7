namespace K7.Clients.Shared.Interfaces;

public interface IAmbientThemeService
{
    Guid? CurrentMediaId { get; }

    /// <summary>
    /// True when the current media theme reached natural end and must not auto-restart
    /// until the theme context is cleared or a different media starts.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Starts or keeps theme playback for <paramref name="mediaId"/>. Same media is a no-op
    /// (including after natural end); a different media crossfades when something is already playing.
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
    /// Fades out immediately (watch / trailer / hard leave).
    /// </summary>
    Task FadeOutAsync(double durationSeconds = 0.5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops playback immediately without fading.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
