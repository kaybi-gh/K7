using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Home;

public sealed record HomeFeedItemDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required MediaType MediaType { get; init; }
    public required string NavigationTarget { get; init; }
    public IReadOnlyList<MetadataPictureDto>? Pictures { get; init; }
    public string? AdditionalInfo { get; init; }
    public int GroupCount { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public bool Watched { get; init; }
    public double Progress { get; init; }

    // Detailed fields (populated only when Detailed=true)
    public string? Overview { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? ContentRating { get; init; }
    public int? RuntimeMinutes { get; init; }
    public double? Rating { get; init; }
}
