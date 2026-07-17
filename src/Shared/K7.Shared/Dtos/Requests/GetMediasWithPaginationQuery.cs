using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetMediasWithPaginationQuery
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid[]? Ids { get; init; }
    public bool? ContinueWatching { get; init; }
    public Guid[]? PersonIds { get; init; }
    public Guid[]? ArtistIds { get; init; }
    public string[]? Genres { get; init; }
    public HashSet<MediaType>? MediaTypes { get; init; }
    public HashSet<MediaOrderingOption>? OrderBy { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public MediaProvenance? Provenance { get; init; }
    public string? SearchText { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public enum MediaOrderingOption
{
    CreatedAsc,
    CreatedDesc,
    DiscNumberAsc,
    DiscNumberDesc,
    LastInteractedAsc,
    LastInteractedDesc,
    LocalRatingAsc,
    LocalRatingDesc,
    OriginalTitleAsc,
    OriginalTitleDesc,
    PlayCountAsc,
    PlayCountDesc,
    PopularityAsc,
    PopularityDesc,
    ProviderRatingAsc,
    ProviderRatingDesc,
    TrendingAsc,
    TrendingDesc,
    ReleaseDateAsc,
    ReleaseDateDesc,
    TitleAsc,
    TitleDesc,
    TrackNumberAsc,
    TrackNumberDesc,
    RecommendedForYou,
    BecauseYouWatched
}

public enum MediaProvenance
{
    Local,
    Federation
}
