namespace MediaServer.Domain.Entities.Ratings;
public class MetadataRating : BaseRating
{
    public MetadataRating() : base(RatingSource.MetadataProvider) { }

    public required MetadataProvider MetadataProvider { get; set; }
    public int? RatingCount { get; set; }
}
