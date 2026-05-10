namespace K7.Clients.Shared.Models;

public enum MediaCardKind { Poster, Cover, Serie, Season, Episode }

public record MediaCardViewModel
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public MediaCardKind Kind { get; init; }
    public string? Title { get; init; }
    public string? PictureUrl { get; init; }
    public string? BackdropUrl { get; init; }
    public string? AdditionalInformations { get; init; }
    public bool Watched { get; set; } = false;
    public double Progress { get; set; } = 0;
    public int GroupCount { get; set; } = 1;
    public int SerieSeasonCount { get; set; } = 1;
    public int? SerieReleaseYear { get; init; }
    public string? NavigationTarget { get; init; }
}
