namespace K7.Clients.Shared.Domain.Models;

public enum MediaPosterKind { Movie, Album }

public record MediaPosterViewModel
{
    public required string Id { get; init; }
    public MediaPosterKind Kind { get; init; }
    public string? Title { get; init; }
    public string? PosterPictureHref { get; init; }
    public string? AdditionalInformations { get; init; }
    public bool Watched { get; init; } = false;
    public double Progress { get; init; } = 0;
}
