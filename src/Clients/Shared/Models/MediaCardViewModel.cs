using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Models;

public enum MediaCardKind { Poster, Cover, Serie, Season, Episode }

public record MediaCardViewModel
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public MediaCardKind Kind { get; init; }
    public MediaType? MediaType { get; init; }
    public int? UserRating { get; init; }
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

    // Detailed fields (TV hero)
    public string? Overview { get; init; }
    public string? TagLine { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? ContentRating { get; init; }
    public int? RuntimeMinutes { get; init; }
    public double? Rating { get; init; }
    public int? ReleaseYear { get; init; }
}
