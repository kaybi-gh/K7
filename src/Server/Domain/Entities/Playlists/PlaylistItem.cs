using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Playlists;

public class PlaylistItem : BaseEntity
{
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public int Order { get; set; }
}
