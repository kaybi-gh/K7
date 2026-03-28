namespace K7.Server.Domain.ValueObjects;

public class MediaIdentification : ValueObject
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

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Title;
        if (ReleaseYear.HasValue)
            yield return ReleaseYear.Value;
        if (SeriesTitle is not null)
            yield return SeriesTitle;
        if (SeasonNumber.HasValue)
            yield return SeasonNumber.Value;
        if (EpisodeNumber.HasValue)
            yield return EpisodeNumber.Value;
        if (AbsoluteNumber.HasValue)
            yield return AbsoluteNumber.Value;
    }
}
