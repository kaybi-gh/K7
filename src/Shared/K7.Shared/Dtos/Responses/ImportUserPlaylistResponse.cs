namespace K7.Shared.Dtos.Responses;

public sealed record ImportUserPlaylistResponse
{
    public required Guid PlaylistId { get; init; }
    public int AddedItemCount { get; init; }
    public bool WasCreated { get; init; }
}
