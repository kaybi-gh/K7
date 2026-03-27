using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(AudioMediaFormatDto), nameof(AudioMediaFormat))]
[JsonDerivedType(typeof(VideoMediaFormatDto), nameof(VideoMediaFormat))]
public abstract record MediaFormatDto
{
    public required string Id { get; set; }
    public required string Container { get; init; }

}
