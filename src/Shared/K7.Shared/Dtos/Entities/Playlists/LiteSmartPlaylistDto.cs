using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record LiteSmartPlaylistDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public int RuleCount { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }

    public static LiteSmartPlaylistDto FromDomain(SmartPlaylist domain) => new()
    {
        Id = domain.Id,
        Title = domain.Title,
        Description = domain.Description,
        MediaType = domain.MediaType,
        RuleCount = domain.Rules.Count,
        CoverPicture = domain.CoverPicture != null ? MetadataPictureDto.FromDomain(domain.CoverPicture) : null,
        Created = domain.Created,
        LastModified = domain.LastModified
    };
}
