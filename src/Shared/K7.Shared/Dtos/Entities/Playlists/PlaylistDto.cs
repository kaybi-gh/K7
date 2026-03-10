using K7.Server.Domain.Entities.Playlists;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record PlaylistDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Guid UserId { get; init; }
    public bool IsSmartPlaylist { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public int ItemCount { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }

    public static PlaylistDto FromDomain(Playlist domain) => new()
    {
        Id = domain.Id,
        Title = domain.Title,
        Description = domain.Description,
        UserId = domain.UserId,
        IsSmartPlaylist = domain is SmartPlaylist,
        CoverPicture = domain.CoverPicture != null ? MetadataPictureDto.FromDomain(domain.CoverPicture) : null,
        ItemCount = domain.Items.Count,
        Created = domain.Created,
        LastModified = domain.LastModified
    };
}
