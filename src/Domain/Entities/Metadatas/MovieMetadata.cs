namespace MediaServer.Domain.Entities.Metadatas;
public class MovieMetadata() : BaseMetadata(MediaType.Movie)
{
    public required string Title { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TagLine { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }

    public virtual IList<string>? Genres { get; set; }
}
