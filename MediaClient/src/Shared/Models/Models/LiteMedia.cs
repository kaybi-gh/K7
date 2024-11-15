namespace MediaClient.Shared.Domain.Models;

public abstract record LiteMedia
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? PosterPictureHref { get; init; }
    public string? BackgroundPictureHref { get; init; }
}
