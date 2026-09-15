using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Entities.Collections;

public sealed record CollectionDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
    public Guid? UserId { get; init; }
    public MediaType? MediaType { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public bool IsDynamic { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
    public DateTimeOffset? LastEvaluatedAt { get; init; }
    public MetadataPictureDto? CoverPicture { get; init; }
    public int ItemCount { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
