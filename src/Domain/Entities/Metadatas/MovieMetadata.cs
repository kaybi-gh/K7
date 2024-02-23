namespace MediaServer.Domain.Entities.Metadatas;
public class MovieMetadata : BaseMetadata
{
    public MovieMetadata() : base(MediaType.Movie) { }

    public string? Title { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? TagLine { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }
    public ICollection<string> Genres { get; set; } = [];
    public ICollection<MediaPicture> Pictures { get; set; } = [];
}
