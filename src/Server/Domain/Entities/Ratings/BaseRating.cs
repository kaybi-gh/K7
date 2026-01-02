using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Ratings;
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

    public Guid MediaId { get; set; }
    public virtual BaseMedia Media { get; set; } = null!;
}
