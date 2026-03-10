using K7.Server.Domain.Entities.Playlists;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record LitePlaylistDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public int ItemCount { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }

    public static LitePlaylistDto FromDomain(Playlist domain) => new()
    {
        Id = domain.Id,
        Title = domain.Title,
        Description = domain.Description,
        CoverPicture = domain.CoverPicture != null ? MetadataPictureDto.FromDomain(domain.CoverPicture) : null,
        ItemCount = domain.Items.Count,
        Created = domain.Created,
        LastModified = domain.LastModified
    };
}
