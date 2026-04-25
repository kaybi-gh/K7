using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Playlists;

public class Playlist : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required MediaType MediaType { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public MetadataPicture? CoverPicture { get; set; }

    public IList<PlaylistItem> Items { get; set; } = [];
}
