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
    public void ToETag_ShouldDiffer_WhenOnlyUserVersionChanged()
    {
        var invalidator = new MediaQueryCacheInvalidator();
        var before = invalidator.GetVersion("alice").ToETag();

        invalidator.InvalidateUser("alice");

        invalidator.GetVersion("alice").ToETag().Should().NotBe(before);
    }
}
