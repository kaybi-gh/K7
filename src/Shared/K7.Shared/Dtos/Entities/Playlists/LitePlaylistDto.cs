using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record LitePlaylistDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsSmartPlaylist { get; init; }
    public MediaType MediaType { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public IReadOnlyList<MetadataPictureDto> PreviewPictures { get; init; } = [];
    public int ItemCount { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public DateTimeOffset? LastListenedAt { get; init; }

}
