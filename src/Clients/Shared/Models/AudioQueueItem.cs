using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Models;

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
    public string? CoverDominantColor { get; init; }
    public double? Duration { get; init; }
    public Guid? ArtistPersonId { get; init; }
    public string? Genre { get; init; }
    public int? UserRating { get; set; }

    // Audio analysis (for adaptive crossfade & harmonic transitions)
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? Energy { get; init; }
}
