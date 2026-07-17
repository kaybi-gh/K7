using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Shared.Dtos.Requests;

public sealed record QueryMediasRequest
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid[]? Ids { get; init; }
    public HashSet<MediaType>? MediaTypes { get; init; }
    public HashSet<MediaOrderingOption>? OrderBy { get; init; }
    public MediaProvenance? Provenance { get; init; }
    /// <summary>When set, only media whose origin peer matches.</summary>
    public Guid? OriginPeerServerId { get; init; }
    /// <summary>When true, only local-origin media.</summary>
    public bool? LocalOriginOnly { get; init; }
    public string? SearchText { get; init; }
    public RuleGroupDto Filter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}
