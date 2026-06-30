using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record ImportUserPlaylistRequest
{
    public required string Title { get; init; }
    public required MediaType MediaType { get; init; }
    public required IReadOnlyList<Guid> MediaIds { get; init; }
}
