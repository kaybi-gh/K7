using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Entities.Users;

public class UserPlaylistState : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public DateTime? LastListenedAt { get; set; }
}
