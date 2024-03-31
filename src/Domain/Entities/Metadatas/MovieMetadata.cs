namespace MediaServer.Domain.Entities.Metadatas;
public class MovieMetadata() : BaseMetadata(MediaType.Movie)
{
    public string? TagLine { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }

    public virtual IList<string>? Genres { get; set; }
}
