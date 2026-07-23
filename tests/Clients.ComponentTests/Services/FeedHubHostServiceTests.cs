using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class FeedHubHostServiceTests
{
    [Test]
    public void UpdateLocation_ShouldEvictOldestNonHome_WhenMountLimitExceeded()
    {
        var sut = new FeedHubHostService();
        sut.SetEnabled(true);
        sut.SetMountLimit(3);

        var home = "https://k7.local/";
        var g1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var g2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var g3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var g4 = Guid.Parse("44444444-4444-4444-4444-444444444444");

        sut.UpdateLocation(home);
        sut.UpdateLocation($"https://k7.local/library-groups/{g1}");
        sut.UpdateLocation($"https://k7.local/library-groups/{g2}");
        sut.UpdateLocation($"https://k7.local/explore?library-group={g3}");
        sut.UpdateLocation($"https://k7.local/library-groups/{g4}");

        var keys = sut.MountedKeys;
        keys.Should().Contain(FeedHubKey.Home);
        keys.Should().NotContain(FeedHubKey.ForLibraryGroup(g1));
        keys.Should().Contain(FeedHubKey.ForLibraryGroup(g2));
        keys.Should().Contain(FeedHubKey.ForExploreGroup(g3));
        keys.Should().Contain(FeedHubKey.ForLibraryGroup(g4));
        keys.Count(k => k.Kind != FeedHubKind.Home).Should().Be(3);
    }

    [Test]
    public void UpdateLocation_ShouldNotEvictHome_WhenMountLimitExceeded()
    {
        var sut = new FeedHubHostService();
        sut.SetEnabled(true);
        sut.SetMountLimit(1);

        sut.UpdateLocation("https://k7.local/");
        sut.UpdateLocation($"https://k7.local/library-groups/{Guid.NewGuid()}");
        sut.UpdateLocation($"https://k7.local/library-groups/{Guid.NewGuid()}");

        sut.MountedKeys.Should().Contain(FeedHubKey.Home);
        sut.MountedKeys.Count(k => k.Kind != FeedHubKind.Home).Should().Be(1);
    }

    [Test]
    public void UpdateLocation_ShouldRefreshLruOrder_WhenRevisitingKey()
    {
        var sut = new FeedHubHostService();
        sut.SetEnabled(true);
        sut.SetMountLimit(2);

        var g1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var g2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var g3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

        sut.UpdateLocation($"https://k7.local/library-groups/{g1}");
        sut.UpdateLocation($"https://k7.local/library-groups/{g2}");
        // Touch g1 so g2 becomes the oldest non-home.
        sut.UpdateLocation($"https://k7.local/library-groups/{g1}");
        sut.UpdateLocation($"https://k7.local/library-groups/{g3}");

        sut.MountedKeys.Should().NotContain(FeedHubKey.ForLibraryGroup(g2));
        sut.MountedKeys.Should().Contain(FeedHubKey.ForLibraryGroup(g1));
        sut.MountedKeys.Should().Contain(FeedHubKey.ForLibraryGroup(g3));
    }
}
