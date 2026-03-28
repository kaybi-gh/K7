namespace K7.Clients.Shared.Models;

public enum MediaCardKind { Poster, Cover, Serie }

public record MediaCardViewModel
{
    public required string Id { get; init; }
    public MediaCardKind Kind { get; init; }
    public string? Title { get; init; }
    public string? PictureUrl { get; init; }
    public string? AdditionalInformations { get; init; }
    public bool Watched { get; init; } = false;
    public double Progress { get; init; } = 0;
}
