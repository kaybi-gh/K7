using K7.Server.Domain.Entities.Playlists;
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

    public static SmartPlaylistDto FromDomain(SmartPlaylist domain) => new()
    {
        Id = domain.Id,
        Title = domain.Title,
        Description = domain.Description,
        UserId = domain.UserId,
        MediaType = domain.MediaType,
        MatchCondition = domain.MatchCondition,
        Rules = domain.Rules.Select(r => new SmartPlaylistRuleDto
        {
            Field = r.Field,
            Operator = r.Operator,
            Value = r.Value
        }).ToList(),
        Limit = domain.Limit,
        OrderBy = domain.OrderBy,
        OrderDescending = domain.OrderDescending,
        CoverPicture = domain.CoverPicture != null ? MetadataPictureDto.FromDomain(domain.CoverPicture) : null,
        ItemCount = domain.Items.Count,
        LastEvaluatedAt = domain.LastEvaluatedAt,
        Created = domain.Created,
        LastModified = domain.LastModified
    };
}
