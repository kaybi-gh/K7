namespace K7.Shared.Dtos.Requests;

public sealed record CreatePlaylistRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
}
