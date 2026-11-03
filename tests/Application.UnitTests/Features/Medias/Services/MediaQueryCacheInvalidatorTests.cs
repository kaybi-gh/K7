using K7.Server.Application.Features.Medias.Services;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MediaQueryCacheInvalidatorTests
{
    [Test]
    public void InvalidateUser_ShouldChangeOnlyThatUserVersion_WhenCalled()
    {
        var invalidator = new MediaQueryCacheInvalidator();
        var before = invalidator.GetVersion("alice");
        var otherBefore = invalidator.GetVersion("bob");

        invalidator.InvalidateUser("alice");

        invalidator.GetVersion("alice").Should().NotBe(before);
        invalidator.GetVersion("bob").Should().Be(otherBefore);
        invalidator.Version.Should().Be(before.Catalog);
    }

    [Test]
    public void InvalidateAll_ShouldChangeEveryUserVersion_WhenCalled()
    {
        var invalidator = new MediaQueryCacheInvalidator();
        invalidator.InvalidateUser("alice");
        var alice = invalidator.GetVersion("alice");
        var bob = invalidator.GetVersion("bob");
        var anonymous = invalidator.GetVersion(null);

        invalidator.InvalidateAll();

        invalidator.GetVersion("alice").Should().NotBe(alice);
        invalidator.GetVersion("bob").Should().NotBe(bob);
        invalidator.GetVersion(null).Should().NotBe(anonymous);
    }

    [Test]
    public void GetETag_ShouldDiffer_WhenOnlyUserVersionChanged()
    {
        var invalidator = new MediaQueryCacheInvalidator();
        var before = invalidator.GetETag("alice", null);

        invalidator.InvalidateUser("alice");

        invalidator.GetETag("alice", null).Should().NotBe(before);
    }

    [Test]
    public void GetETag_ShouldDiffer_WhenUsersShareTheSameVersions()
    {
        var invalidator = new MediaQueryCacheInvalidator();

        invalidator.GetVersion("alice").Should().Be(invalidator.GetVersion("bob"));
        invalidator.GetETag("alice", null).Should().NotBe(invalidator.GetETag("bob", null));
    }

    [Test]
    public void GetETag_ShouldDiffer_WhenSharedProfileChanged()
    {
        var invalidator = new MediaQueryCacheInvalidator();

        invalidator.GetETag("alice", Guid.NewGuid()).Should().NotBe(invalidator.GetETag("alice", null));
    }

    [Test]
    public void GetETag_ShouldDiffer_WhenServerRestartedWithTheSameVersions()
    {
        var beforeRestart = new MediaQueryCacheInvalidator();
        var afterRestart = new MediaQueryCacheInvalidator();

        afterRestart.GetETag("alice", null).Should().NotBe(beforeRestart.GetETag("alice", null));
    }

    [Test]
    public void GetETag_ShouldBeStable_WhenNothingChanged()
    {
        var invalidator = new MediaQueryCacheInvalidator();
        var profileId = Guid.NewGuid();

        invalidator.GetETag("alice", profileId).Should().Be(invalidator.GetETag("alice", profileId));
    }
}
