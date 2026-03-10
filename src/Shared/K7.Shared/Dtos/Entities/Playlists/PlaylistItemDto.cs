using K7.Server.Domain.Entities.Playlists;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record PlaylistItemDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int Order { get; init; }
    public string? MediaTitle { get; init; }
    public double? Duration { get; init; }

    public static PlaylistItemDto FromDomain(PlaylistItem domain) => new()
    {
        Id = domain.Id,
        MediaId = domain.MediaId,
        Order = domain.Order,
        MediaTitle = domain.Media?.Title,
        Duration = null
    };
}
