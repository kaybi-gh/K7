using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Events;

public class DynamicPlaylistCreatedEvent(DynamicPlaylist dynamicPlaylist) : BaseEvent
{
    public DynamicPlaylist DynamicPlaylist { get; } = dynamicPlaylist;
}
