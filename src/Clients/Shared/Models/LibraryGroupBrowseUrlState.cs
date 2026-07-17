using K7.Clients.Shared.Enums;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.Shared.Models;

public sealed record LibraryGroupBrowseUrlState(
    MediaType? MediaType = null,
    MediaOrderingOption? Sort = null,
    BrowseViewMode? View = null,
    RuleGroupDto? Filter = null,
    IntelligentSearchRequest? IntelligentSearch = null,
    string? ContentSource = null);
