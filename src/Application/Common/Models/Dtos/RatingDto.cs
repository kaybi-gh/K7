using MediaServer.Domain.Enums;

namespace MediaServer.Application.Common.Models.Dtos;

public record RatingDto
{
    public Guid Id { get; init; }
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}
