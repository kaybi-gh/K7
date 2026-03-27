using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record SmartPlaylistDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Guid UserId { get; init; }
    public MediaType? MediaType { get; init; }
    public SmartPlaylistMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<SmartPlaylistRuleDto> Rules { get; init; } = [];
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public int ItemCount { get; init; }
    public DateTimeOffset? LastEvaluatedAt { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }

}
