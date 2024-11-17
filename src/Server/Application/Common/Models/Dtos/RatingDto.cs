using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Models.Dtos;

public record RatingDto
{
    public Guid Id { get; init; }
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}
