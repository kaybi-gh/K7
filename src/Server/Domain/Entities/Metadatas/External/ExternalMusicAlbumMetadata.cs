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
}

public class ExternalMusicTrackMetadata
{
    public required string Title { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? MusicBrainzRecordingId { get; init; }
}
