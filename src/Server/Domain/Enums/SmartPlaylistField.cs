namespace K7.Server.Domain.Enums;

public enum SmartPlaylistField
{
    // Common (all media types)
    Title,
    Genre,
    Year,
    Rating,
    PlayCount,
    DateAdded,
    LastPlayed,
    IsCompleted,

    // MusicTrack
    ArtistName,
    AlbumTitle,
    TrackNumber,
    DiscNumber,
    Bpm,
    Duration,

    // Movie
    OriginalLanguage
}
