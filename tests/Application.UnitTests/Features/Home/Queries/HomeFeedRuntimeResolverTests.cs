using K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.UnitTests.Features.Home.Queries;

public class HomeFeedRuntimeResolverTests
{
    private static readonly IReadOnlyDictionary<Guid, int> EmptyMinutes = new Dictionary<Guid, int>();

    [Test]
    public void Merge_ShouldKeepExistingRuntime_WhenAlreadySet()
    {
        var movieId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(movieId, MediaType.Movie, runtimeMinutes: 99)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            new Dictionary<Guid, int> { [movieId] = 125 },
            EmptyMinutes,
            EmptyMinutes);

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().Be(99);
    }

    [Test]
    public void Merge_ShouldApplyMovieFileDuration_WhenMissing()
    {
        var movieId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(movieId, MediaType.Movie)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            new Dictionary<Guid, int> { [movieId] = 125 },
            EmptyMinutes,
            EmptyMinutes);

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().Be(125);
    }

    [Test]
    public void Merge_ShouldApplySerieEpisodeRuntime_WhenMissing()
    {
        var serieId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(serieId, MediaType.Serie)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            EmptyMinutes,
            new Dictionary<Guid, int> { [serieId] = 47 },
            EmptyMinutes);

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().Be(47);
    }

    [Test]
    public void Merge_ShouldFallBackToSerieRuntime_WhenSeasonCardUsesSerieId()
    {
        var serieId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(serieId, MediaType.SerieSeason)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            EmptyMinutes,
            new Dictionary<Guid, int> { [serieId] = 42 },
            EmptyMinutes);

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().Be(42);
    }

    [Test]
    public void Merge_ShouldPreferSeasonRuntime_WhenSeasonIdMatches()
    {
        var seasonId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(seasonId, MediaType.SerieSeason)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            EmptyMinutes,
            new Dictionary<Guid, int> { [seasonId] = 50 },
            new Dictionary<Guid, int> { [seasonId] = 41 });

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().Be(41);
    }

    [Test]
    public void Merge_ShouldIgnoreMusic_WhenNoRuntime()
    {
        var albumId = Guid.NewGuid();
        var items = new[]
        {
            CreateItem(albumId, MediaType.MusicAlbum)
        };

        var result = HomeFeedRuntimeResolver.Merge(
            items,
            new Dictionary<Guid, int> { [albumId] = 44 },
            EmptyMinutes,
            EmptyMinutes);

        result.Should().ContainSingle().Which.RuntimeMinutes.Should().BeNull();
    }

    private static HomeFeedItemDto CreateItem(Guid id, MediaType mediaType, int? runtimeMinutes = null) => new()
    {
        Id = id,
        Title = "Title",
        MediaType = mediaType,
        NavigationTarget = "/",
        RuntimeMinutes = runtimeMinutes
    };
}
