namespace MediaClient.Shared.Domain.Models;

public record Movie
{
    public required string Id { get; init; }
    public string Title { get; init; } = null!;
    public string? PosterPictureHref { get; init; }
    public string? BackgroundPictureHref { get; init; }
    public string AdditionalInformations { get; init; } = "";
    public bool Watched { get; init; } = false;
    public double Progress { get; init; } = 0;
    public int Duration { get; init; } = 0;
    public int Rating { get; init; } = 0;
    public string Synopsis { get; init; } = "";
    public List<LitePersonRole> Casting { get; init; } = [];
    public List<string> Genres { get; init; } = [];
    public Dictionary<string, string> Sources { get; init; } = [];
}
