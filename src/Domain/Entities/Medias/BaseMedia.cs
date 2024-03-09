using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Ratings;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type, MediaIdentification identification) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;
    public MediaIdentification Identification { get; protected set; } = identification;
    public virtual BaseMetadata? Metadata { get; set; }

    public required int LibraryId { get; set; }
    public virtual required Library Library { get; set; }
    public virtual ICollection<IndexedFile> IndexedFiles { get; set; } = [];
    public virtual ICollection<BaseRating> Ratings { get; set; } = [];
    public virtual ICollection<ExternalId> ExternalIds { get; set; } = [];
}
