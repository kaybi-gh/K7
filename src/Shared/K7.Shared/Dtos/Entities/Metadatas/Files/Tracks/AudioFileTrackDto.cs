using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

public sealed record AudioFileTrackDto : FileTrackDto
{
    public string? Name { get; init; }
    public string? Language { get; init; }
    public required string Codec { get; init; }
    public required int Channels {  get; init; }
    public string? ChannelLayout { get; init; }
    public int? SampleRateHz { get; init; }
    public string? Profile { get; init; }

    public static AudioFileTrackDto FromDomain(AudioFileTrack domain) => new()
    {
        Index = domain.Index,
        IsDefault = domain.IsDefault,
        Name = domain.Name,
        Language = domain.Language,
        Codec = domain.Codec,
        Channels = domain.Channels,
        ChannelLayout = domain.ChannelLayout,
        SampleRateHz = domain.SampleRateHz,
        Profile = domain.Profile
    };
}
