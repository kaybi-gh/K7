using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Events;

public class SmartPlaylistUpdatedEvent(SmartPlaylist smartPlaylist) : BaseEvent
{
    public SmartPlaylist SmartPlaylist { get; } = smartPlaylist;
}
