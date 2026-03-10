using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Events;

public class PlaylistItemRemovedEvent(Playlist playlist, PlaylistItem item) : BaseEvent
{
    public Playlist Playlist { get; } = playlist;
    public PlaylistItem Item { get; } = item;
}
