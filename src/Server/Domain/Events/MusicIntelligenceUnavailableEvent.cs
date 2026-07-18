namespace K7.Server.Domain.Events;

/// <summary>
/// Raised when AudioMuse transitions from reachable to unreachable.
/// </summary>
public class MusicIntelligenceUnavailableEvent : BaseEvent
{
    public MusicIntelligenceUnavailableEvent(string? reason = null)
    {
        Reason = reason;
    }

    public string? Reason { get; }
}
