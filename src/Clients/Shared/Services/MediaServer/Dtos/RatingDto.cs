using System.Text.Json.Serialization;

namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record RatingDto
{
    public Guid Id { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}
