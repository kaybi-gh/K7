using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreatePlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
}
