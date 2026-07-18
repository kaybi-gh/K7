using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Entities.SharedProfiles;

/// <summary>
/// Links a playlist to a shared profile so all members can see it in their library navigation.
/// </summary>
public class SharedProfilePlaylist : BaseAuditableEntity
{
    public Guid SharedProfileId { get; set; }
    public SharedProfile SharedProfile { get; set; } = null!;

    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;
}
