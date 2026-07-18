using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

public record GetHomeFeedItemsQuery : IRequest<PaginatedList<HomeFeedItemDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public bool? ContinueWatching { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MediaOrderingOption>? OrderBy { get; init; }
    public bool? Detailed { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
}

public class GetHomeFeedItemsQueryHandler(IApplicationDbContext context, IUser currentUser, IBoundedMemoryCache cache, IMediaQueryCacheInvalidator cacheInvalidator, MediaAccessFilter mediaAccessFilter, IPlaybackPolicySettingsProvider playbackPolicySettingsProvider, IHomeRecommendationService homeRecommendationService)
    : IRequestHandler<GetHomeFeedItemsQuery, PaginatedList<HomeFeedItemDto>>
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan RecommendedCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ContinueWatchingCacheDuration = TimeSpan.FromMinutes(5);

    private readonly HomeFeedContinueWatchingStrategy _continueWatchingStrategy = new(context, playbackPolicySettingsProvider, mediaAccessFilter);
    private readonly HomeFeedRecentlyAddedStrategy _recentlyAddedStrategy = new(context, mediaAccessFilter);
    private readonly HomeFeedTopLevelStrategy _topLevelStrategy = new(context, mediaAccessFilter);
    private readonly HomeFeedRecommendedStrategy _recommendedStrategy = new(context, homeRecommendationService);
    private readonly HomeFeedBecauseYouWatchedStrategy _becauseYouWatchedStrategy = new(context, homeRecommendationService);

    public async Task<PaginatedList<HomeFeedItemDto>> Handle(GetHomeFeedItemsQuery request, CancellationToken cancellationToken)
    {
        request = request with
        {
            PageNumber = PagingDefaults.ClampPageNumber(request.PageNumber),
            PageSize = PagingDefaults.ClampPageSize(request.PageSize)
        };

        var userId = await currentUser.GetIdAsync(cancellationToken);

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var strategy = InferStrategy(request);
        VideoPlaybackPolicySettingsDto? continueWatchingPolicy = null;
        if (strategy == FeedStrategy.ContinueWatching && userId.HasValue)
            continueWatchingPolicy = await playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(userId.Value, cancellationToken);

        var cacheKey = BuildCacheKey(request, userId, continueWatchingPolicy);
        var version = cacheInvalidator.Version;

        if (cache.TryGetValue(cacheKey, out var cachedValue)
            && cachedValue is (long cachedVersion, PaginatedList<HomeFeedItemDto> cachedResult)
            && cachedVersion == version)
            return cachedResult;

        var result = strategy switch
        {
            FeedStrategy.ContinueWatching => await _continueWatchingStrategy.HandleAsync(request, userId, cancellationToken),
            FeedStrategy.RecentlyAdded => await _recentlyAddedStrategy.HandleAsync(request, userId, cancellationToken),
            FeedStrategy.RecommendedForYou => await _recommendedStrategy.HandleAsync(request, userId, cancellationToken),
            FeedStrategy.BecauseYouWatched => await _becauseYouWatchedStrategy.HandleAsync(request, userId, cancellationToken),
            _ => await _topLevelStrategy.HandleAsync(request, userId, cancellationToken)
        };

        var ttl = strategy switch
        {
            FeedStrategy.ContinueWatching => ContinueWatchingCacheDuration,
            FeedStrategy.RecommendedForYou => RecommendedCacheDuration,
            FeedStrategy.BecauseYouWatched => RecommendedCacheDuration,
            _ => DefaultCacheDuration
        };
        cache.SetWithSize(cacheKey, (version, result), ttl);
        return result;
    }

    private static FeedStrategy InferStrategy(GetHomeFeedItemsQuery request)
    {
        if (request.ContinueWatching == true)
            return FeedStrategy.ContinueWatching;

        if (request.OrderBy is { Count: > 0 } && request.OrderBy.Contains(MediaOrderingOption.RecommendedForYou))
            return FeedStrategy.RecommendedForYou;

        if (request.OrderBy is { Count: > 0 } && request.OrderBy.Contains(MediaOrderingOption.BecauseYouWatched))
            return FeedStrategy.BecauseYouWatched;

        if (request.OrderBy is { Count: > 0 } && request.OrderBy.Contains(MediaOrderingOption.CreatedDesc))
            return FeedStrategy.RecentlyAdded;

        return FeedStrategy.TopLevel;
    }

    private static string BuildCacheKey(GetHomeFeedItemsQuery request, Guid? userId, VideoPlaybackPolicySettingsDto? continueWatchingPolicy = null)
    {
        var parts = new List<string> { "home-feed", $"u:{userId}" };

        if (request.LibraryIds is { Length: > 0 })
            parts.Add($"lib:{string.Join(',', request.LibraryIds.OrderBy(x => x))}");
        if (request.LibraryGroupIds is { Length: > 0 })
            parts.Add($"lg:{string.Join(',', request.LibraryGroupIds.OrderBy(x => x))}");
        if (request.ContinueWatching.HasValue)
            parts.Add($"cw:{request.ContinueWatching.Value}");
        if (continueWatchingPolicy is not null)
            parts.Add($"cwMin:{continueWatchingPolicy.MinResumePercent}:{continueWatchingPolicy.MinResumeDurationSeconds}:{continueWatchingPolicy.ContinueWatchingMaxAgeDays}");
        if (request.MediaTypes is { Count: > 0 })
            parts.Add($"mt:{string.Join(',', request.MediaTypes.Order())}");
        if (request.OrderBy is { Count: > 0 })
            parts.Add($"ob:{string.Join(',', request.OrderBy.Order())}");
        if (request.Detailed == true)
            parts.Add("detailed");
        parts.Add($"p:{request.PageNumber}");
        parts.Add($"ps:{request.PageSize}");

        return string.Join('|', parts);
    }

    private enum FeedStrategy
    {
        ContinueWatching,
        RecentlyAdded,
        RecommendedForYou,
        BecauseYouWatched,
        TopLevel
    }
}
