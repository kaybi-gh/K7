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

}
