using K7.Server.Domain.Interfaces;

namespace K7.Server.Domain.Entities.Metadatas.External;

public class ExternalMusicAlbumMetadata : IExternalMetadata
{
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? Overview { get; init; }

    public IList<string> Genres { get; init; } = [];
    public IList<ExternalId> ExternalIds { get; init; } = [];
    public IList<MetadataPicture> Pictures { get; init; } = [];
    public IList<ExternalMusicTrackMetadata> Tracks { get; init; } = [];
    public IList<ExternalMusicArtistMetadata> Artists { get; init; } = [];
}

public class ExternalMusicTrackMetadata
{
    public Guid? RemoteId { get; init; }
    public required string Title { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? MusicBrainzRecordingId { get; init; }
    public string? Isrc { get; init; }
    public string? Lyrics { get; init; }
    public string? LyricsLrc { get; init; }
    public IList<ExternalMusicTrackArtistCredit> ArtistCredits { get; init; } = [];
}

public class ExternalMusicTrackArtistCredit
{
    public required string Name { get; init; }
    public required string MusicBrainzArtistId { get; init; }
    public bool IsGuest { get; init; }
}

public class ExternalMusicArtistMetadata
{
    public required string Name { get; init; }
    public required string MusicBrainzArtistId { get; init; }
}
