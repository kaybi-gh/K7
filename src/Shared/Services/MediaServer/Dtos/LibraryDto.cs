using System.Text.Json.Serialization;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record LibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
}
