namespace K7.Server.Domain.Entities.Ratings;
public class MetadataProviderRating : BaseRating // TODO - Use tags instead?
{
    public MetadataProviderRating() : base(RatingSource.MetadataProvider) { }

    public required MetadataProvider MetadataProvider { get; set; }
    public int? RatingCount { get; set; }
}
