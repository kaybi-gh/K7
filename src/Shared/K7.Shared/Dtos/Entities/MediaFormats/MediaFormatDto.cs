using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(AudioMediaFormatDto), nameof(AudioMediaFormat))]
[JsonDerivedType(typeof(VideoMediaFormatDto), nameof(VideoMediaFormat))]
public abstract record MediaFormatDto
{
    public required string Id { get; set; }
    public required string Container { get; init; }

    public static MediaFormatDto FromDomain(BaseMediaFormat domain) => domain switch
    {
        AudioMediaFormat audioMediaFormat => new AudioMediaFormatDto()
        {
            Id = domain.Id,
            Container = domain.Container,
            Codec = audioMediaFormat.Codec
        },
        VideoMediaFormat videoMediaFormat => new VideoMediaFormatDto()
        {
            Id = domain.Id,
            Container = domain.Container,
            AudioCodec = videoMediaFormat.AudioCodec,
            VideoCodec = videoMediaFormat.VideoCodec
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
