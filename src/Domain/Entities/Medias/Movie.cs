namespace MediaServer.Domain.Entities.Medias;
public class Movie : BaseMedia
{
    public Movie() : base(MediaType.Movie) { }

    public required string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }
}
