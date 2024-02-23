using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities.Ratings;
public abstract class BaseRating
{
    protected BaseRating(RatingSource source)
    {
        Source = source;
    }

    public RatingSource Source { get; protected set; }
    public double Value { get; set; }
    public double MinimumValue { get; set; }
    public double MaximumValue { get; set; }

    public virtual required BaseMedia Media { get; set; }
}
