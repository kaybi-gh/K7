using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using System.Text.Json.Serialization;

namespace K7.Shared.Dtos.Entities.Metadatas.Files;

[JsonDerivedType(typeof(AudioFileMetadataDto), nameof(AudioFileMetadata))]
[JsonDerivedType(typeof(VideoFileMetadataDto), nameof(VideoFileMetadata))]
public abstract record FileMetadataDto
{
    public Guid Id { get; init; }
    public required string Container { get; init; }

}
