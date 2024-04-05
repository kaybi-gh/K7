using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Domain.Entities.Metadatas.Medias;
public abstract class BaseMediaMetadata(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;

    public required string Title { get; set; }
    public required string OriginalTitle { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    public required int MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
    public virtual ICollection<ExternalId>? ExternalIds { get; set; }
    public virtual ICollection<MetadataPicture>? Pictures { get; set; }
    public virtual ICollection<BaseRating>? Ratings { get; set; }
    public virtual ICollection<BasePersonRole>? Credits { get; set; }
}
