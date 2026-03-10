using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Events;

public class PlaylistUpdatedEvent(Playlist playlist) : BaseEvent
{
    public Playlist Playlist { get; } = playlist;
}
