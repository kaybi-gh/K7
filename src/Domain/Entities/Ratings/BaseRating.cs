using MediaServer.Domain.Entities.Metadatas.Medias;

namespace MediaServer.Domain.Entities.Ratings;
public abstract class BaseRating : BaseAuditableEntity
{
    protected BaseRating(RatingSource source)
    {
        Source = source;
    }

    public RatingSource Source { get; protected set; }
    public double Value { get; set; }
    public double MinimumValue { get; set; }
    public double MaximumValue { get; set; }

    public Guid MetadataId { get; set; }
    public virtual BaseMediaMetadata Metadata { get; set; } = null!;
}
