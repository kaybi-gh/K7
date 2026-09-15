using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Entities.Collections;

public class Collection : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Nobody;
    public MediaType? MediaType { get; set; }
    public Guid? LibraryGroupId { get; set; }
    public LibraryGroup? LibraryGroup { get; set; }
    public RuleGroup? RuleFilter { get; set; }
    public int? Limit { get; set; }
    public DynamicPlaylistOrderBy OrderBy { get; set; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; set; } = true;
    public DateTimeOffset? LastEvaluatedAt { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public MetadataPicture? CoverPicture { get; set; }

    public IList<CollectionItem> Items { get; set; } = [];
}
