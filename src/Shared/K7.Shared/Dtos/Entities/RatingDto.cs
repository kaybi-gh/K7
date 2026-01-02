using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record RatingDto
{
    public Guid Id { get; init; }
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }

    public static RatingDto FromDomain(BaseRating domain) => domain switch
    {
        MetadataProviderRating metadataProviderRating => new RatingDto()
        {
            Id = domain.Id,
            Source = domain.Source,
            Value = domain.Value,
            MinimumValue = domain.MinimumValue,
            MaximumValue = domain.MaximumValue
        },
        UserRating userRating => new RatingDto()
        {
            Id = domain.Id,
            Source = domain.Source,
            Value = domain.Value,
            MinimumValue = domain.MinimumValue,
            MaximumValue = domain.MaximumValue

        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
