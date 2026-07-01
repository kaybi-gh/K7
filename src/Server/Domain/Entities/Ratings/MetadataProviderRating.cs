namespace K7.Server.Domain.Entities.Ratings;
public class MetadataProviderRating : BaseRating
{
    public MetadataProviderRating() : base(RatingSource.MetadataProvider) { }

    public required MetadataProvider MetadataProvider { get; set; }
    public int? RatingCount { get; set; }
}
