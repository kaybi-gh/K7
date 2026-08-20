namespace K7.Server.Domain.Events;

/// <summary>
/// Raised when an existing episode gains its first playable file (local or remote).
/// Distinct from <see cref="MediaCreatedEvent"/> so "Media added" notifications are not repeated.
/// </summary>
public class SerieEpisodeBecamePlayableEvent : BaseEvent
{
    public SerieEpisodeBecamePlayableEvent(Guid episodeId)
    {
        EpisodeId = episodeId;
    }

    public Guid EpisodeId { get; }
}
