namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Counts the episodes auto-played in a row without user interaction, so the next-episode
/// offer can ask "still watching?" instead of counting down again. Shared by the Blazor and
/// native video overlays.
/// </summary>
public sealed class InactivityPromptTracker
{
    private readonly Lock _lock = new();
    private int _unattendedEpisodes;
    private Guid? _autoAdvancedMediaId;

    /// <summary>The countdown ran out and started <paramref name="nextMediaId"/> on its own.</summary>
    public void RegisterAutoAdvance(Guid nextMediaId)
    {
        lock (_lock)
        {
            _unattendedEpisodes++;
            _autoAdvancedMediaId = nextMediaId;
        }
    }

    /// <summary>The user touched the player (key, pointer, tap, explicit play): restart the count.</summary>
    public void RegisterUserActivity()
    {
        lock (_lock)
        {
            _unattendedEpisodes = 0;
            _autoAdvancedMediaId = null;
        }
    }

    /// <summary>
    /// True when the episode that just ended was reached by <paramref name="threshold"/> or more
    /// unattended auto-plays. An episode the user started themself restarts the count.
    /// </summary>
    public bool ShouldPrompt(int threshold, Guid endedMediaId)
    {
        lock (_lock)
        {
            if (_autoAdvancedMediaId != endedMediaId)
            {
                _unattendedEpisodes = 0;
                _autoAdvancedMediaId = null;
                return false;
            }

            return threshold > 0 && _unattendedEpisodes >= threshold;
        }
    }
}
