using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class HomeFeedStoreTests
{
    [Test]
    public void BuildFeedCacheKey_ShouldIncludeIdentity_WhenUsersDiffer()
    {
        var a = HomeFeedStore.BuildFeedCacheKey("user-a", null, "Keep watching", true);
        var b = HomeFeedStore.BuildFeedCacheKey("user-b", null, "Keep watching", true);

        a.Should().NotBe(b);
        a.Should().Contain("user-a");
        b.Should().Contain("user-b");
    }

    [Test]
    public void BuildFeedCacheKey_ShouldIncludeSharedProfile_WhenActive()
    {
        var profileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var personal = HomeFeedStore.BuildFeedCacheKey("user-a", null, "Keep watching", true);
        var shared = HomeFeedStore.BuildFeedCacheKey("user-a", profileId, "Keep watching", true);

        personal.Should().Contain("personal");
        shared.Should().Contain(profileId.ToString("N"));
        personal.Should().NotBe(shared);
    }

    [Test]
    public async Task EnsureLoadedAsync_ShouldReloadRows_WhenIdentityUserIdChanges()
    {
        var itemA = CreateItem("Movie A");
        var itemB = CreateItem("Movie B");
        var media = Substitute.For<IMediaService>();
        media.GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => FeedPage(itemA),
                _ => FeedPage(itemB));

        using var sut = CreateStore(media);

        await sut.EnsureLoadedAsync(canTrackProgress: true, identityUserId: "user-a");
        sut.Rows.Should().ContainSingle()
            .Which.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Movie A");

        await sut.EnsureLoadedAsync(canTrackProgress: true, identityUserId: "user-b");
        sut.Rows.Should().ContainSingle()
            .Which.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Movie B");
    }

    [Test]
    public async Task EnsureLoadedAsync_ShouldNotReload_WhenIdentityStaysTheSame()
    {
        var prefs = Substitute.For<IUserPreferencesService>();
        prefs.GetHomeLayoutAsync(Arg.Any<CancellationToken>()).Returns(CreateLayout());
        var media = Substitute.For<IMediaService>();
        media.GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => FeedPage(CreateItem("Movie A")));

        using var sut = CreateStore(media, prefs);

        await sut.EnsureLoadedAsync(canTrackProgress: true, identityUserId: "user-a");
        await sut.EnsureLoadedAsync(canTrackProgress: true, identityUserId: "user-a");

        await prefs.Received(1).GetHomeLayoutAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnsureLoadedAsync_ShouldDedupeCarouselItems_WhenFeedReturnsDuplicateIds()
    {
        var id = Guid.Parse("2b663634-6bb9-4ebd-924d-c8a18d29181e");
        var first = CreateItem("Heroes") with { Id = id };
        var duplicate = CreateItem("Heroes") with { Id = id };
        var media = Substitute.For<IMediaService>();
        media.GetHomeFeedAsync(Arg.Any<GetHomeFeedQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => FeedPage(first, duplicate));

        using var sut = CreateStore(media);

        await sut.EnsureLoadedAsync(canTrackProgress: true, identityUserId: "user-a");

        sut.Rows.Should().ContainSingle()
            .Which.Items.Should().ContainSingle()
            .Which.Id.Should().Be(id.ToString());
    }

    private static HomeFeedStore CreateStore(
        IMediaService media,
        IUserPreferencesService? prefs = null)
    {
        prefs ??= Substitute.For<IUserPreferencesService>();
        prefs.GetHomeLayoutAsync(Arg.Any<CancellationToken>()).Returns(CreateLayout());

        var api = Substitute.For<IK7ServerService>();
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IUserPreferencesService)).Returns(prefs);
        provider.GetService(typeof(IMediaService)).Returns(media);
        provider.GetService(typeof(IK7ServerService)).Returns(api);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var device = Substitute.For<IDeviceService>();
        device.GetDeviceTypeAsync().Returns(DeviceType.Phone);

        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);

        var session = Substitute.For<ISharedProfileSessionService>();
        session.ActiveGroupId.Returns((Guid?)null);

        return new HomeFeedStore(
            scopeFactory,
            new K7HubClient(NullLogger<K7HubClient>.Instance),
            new MediaCacheStore(),
            device,
            connectivity,
            session,
            NullLogger<HomeFeedStore>.Instance);
    }

    private static HomeLayoutDto CreateLayout() => new()
    {
        Rows =
        [
            new HomeRowConfigDto
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Keep watching",
                DisplayType = HomeRowDisplayType.Carousel,
                PageSize = 20,
                ContinueWatching = true,
                IsVisible = true,
                Order = 0
            }
        ]
    };

    private static HomeFeedItemDto CreateItem(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        MediaType = MediaType.Movie,
        NavigationTarget = "/movies/1"
    };

    private static PaginatedListDto<HomeFeedItemDto> FeedPage(params HomeFeedItemDto[] items) => new()
    {
        Items = [.. items]
    };
}
