using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Domain.Entities.Metadatas;
public abstract class BaseMetadata(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;

    public required int MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
    public virtual ICollection<ExternalId>? ExternalIds { get; set; }
    public virtual ICollection<MediaPicture>? Pictures { get; set; }
    public virtual ICollection<BaseRating>? Ratings { get; set; }
}
