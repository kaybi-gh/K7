using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

[JsonDerivedType(typeof(AudioFileTrackDto), nameof(AudioFileTrack))]
[JsonDerivedType(typeof(VideoFileTrackDto), nameof(VideoFileTrack))]
[JsonDerivedType(typeof(SubtitleFileTrackDto), nameof(SubtitleFileTrack))]
public abstract record FileTrackDto
{
    public int Index { get; init; }
    public bool IsDefault { get; init; }

}
