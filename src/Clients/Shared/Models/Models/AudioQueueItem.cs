using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Domain.Models;

/// <summary>
/// Represents a track in the audio player queue.
/// </summary>
public class AudioQueueItem
{
    public required Guid IndexedFileId { get; init; }
    public required Guid MediaId { get; init; }
    public required string Title { get; init; }
    public required string? Artist { get; init; }
    public required string? AlbumTitle { get; init; }
    public string? CoverUrl { get; init; }
    public double? Duration { get; init; }
    public Guid? ArtistPersonId { get; init; }
    public string? Genre { get; init; }
}
