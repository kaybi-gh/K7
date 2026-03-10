using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Entities.Playlists;

public class Playlist : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public virtual MetadataPicture? CoverPicture { get; set; }

    public virtual IList<PlaylistItem> Items { get; set; } = [];
}
