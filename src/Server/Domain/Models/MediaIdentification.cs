namespace K7.Server.Domain.Models;

public class MediaIdentification
{
    public string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    // Music-specific
    public int? TrackNumber { get; set; }
    public string? AlbumName { get; set; }
    public string? ArtistName { get; set; }

    // Serie-specific
    public string? SeriesTitle { get; set; }
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
    public int? AbsoluteNumber { get; set; }

    public MediaIdentification(string title)
    {
        Title = title;
    }
}
