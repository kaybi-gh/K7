namespace K7.Server.Domain.Models;

public sealed record AudioTagData
{
    public string? Title { get; init; }
    public string? Album { get; init; }
    public IReadOnlyList<string> Artists { get; init; } = [];
    public IReadOnlyList<string> AlbumArtists { get; init; } = [];
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public int? Year { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string? Lyrics { get; init; }
    public double? Bpm { get; init; }
    public byte[]? CoverArtData { get; init; }
    public string? CoverArtMimeType { get; init; }
    public double? ReplayGainTrackGain { get; init; }
    public double? ReplayGainAlbumGain { get; init; }

    /// <summary>MusicBrainz release (album edition) id from tags.</summary>
    public string? MusicBrainzReleaseId { get; init; }

    /// <summary>MusicBrainz release-group id from tags.</summary>
    public string? MusicBrainzReleaseGroupId { get; init; }

    /// <summary>MusicBrainz track artist id from tags.</summary>
    public string? MusicBrainzArtistId { get; init; }

    /// <summary>MusicBrainz album artist id from tags.</summary>
    public string? MusicBrainzAlbumArtistId { get; init; }

    /// <summary>MusicBrainz recording / track id from tags.</summary>
    public string? MusicBrainzRecordingId { get; init; }

    /// <summary>ISRC from tags (ID3 TSRC / iTunes ISRC).</summary>
    public string? Isrc { get; init; }
}
