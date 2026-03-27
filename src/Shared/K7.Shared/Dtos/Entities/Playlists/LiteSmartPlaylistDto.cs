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

}
