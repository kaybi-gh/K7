namespace K7.Server.Domain.Enums;

public enum SmartPlaylistOrderBy
{
    Title = 0,
    DateAdded = 1,
    LastPlayed = 2,
    PlayCount = 3,
    Rating = 4,
    Year = 5,
    Random = 6,

    // MusicTrack (10 reserved: former Bpm)
    ArtistName = 7,
    AlbumTitle = 8,
    TrackNumber = 9,
    Duration = 11
}
