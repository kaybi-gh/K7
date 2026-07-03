using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Federation;

public class VisibilityGrant : BaseEntity
{
    public required Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public FederationContentType? ContentType { get; set; }
    public Guid? PlaylistId { get; set; }
    public Guid? CollectionId { get; set; }

    public Guid? TargetUserId { get; set; }
    public Guid? TargetPeerServerId { get; set; }
    public Guid? TargetOriginUserId { get; set; }
}
