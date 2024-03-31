using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedia;

public record RatingDto
{
    public int Id { get; init; }
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}
