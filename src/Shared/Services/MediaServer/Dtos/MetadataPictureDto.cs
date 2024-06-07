using System.Text.Json.Serialization;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record MetadataPictureDto
{
    public Guid Id { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MetadataPictureType? Type { get; init; }
    public Uri? Uri { get; init; }
}
