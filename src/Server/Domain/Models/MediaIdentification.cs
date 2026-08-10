namespace K7.Server.Domain.Models;

public class MediaIdentification
{
    public string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    // Music-specific
    public int? TrackNumber { get; set; }
    public string? AlbumName { get; set; }
    public string? ArtistName { get; set; }
    public string? MusicBrainzReleaseId { get; set; }
    public string? MusicBrainzReleaseGroupId { get; set; }
    public string? MusicBrainzArtistId { get; set; }
    public string? MusicBrainzAlbumArtistId { get; set; }
    public string? MusicBrainzRecordingId { get; set; }

    // Serie-specific
    public string? SeriesTitle { get; set; }
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
    public int? AbsoluteNumber { get; set; }

    /// <summary>Provider name from path tokens such as [tmdbid-123] (tmdb, tvdb, imdb).</summary>
    public string? ProviderName { get; set; }

    /// <summary>External id from path tokens such as [tmdbid-123].</summary>
    public string? ProviderExternalId { get; set; }

    public MediaIdentification(string title)
    {
        Title = title;
    }
}
