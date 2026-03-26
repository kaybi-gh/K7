using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Restrictions;

public sealed record RestrictedMediaPreviewDto
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public required MediaType Type { get; init; }
    public int? ReleaseYear { get; init; }
}
