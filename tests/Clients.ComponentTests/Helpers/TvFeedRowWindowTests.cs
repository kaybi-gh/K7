using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TvFeedRowWindowTests
{
    [Test]
    public void InitialRange_ShouldKeepActiveAndForwardOverscan()
    {
        var (from, to) = TvFeedRowWindow.InitialRange(0);

        from.Should().Be(0);
        to.Should().Be(2);
    }

    [Test]
    public void InitialRange_ShouldIncludeOneRowBehind()
    {
        var (from, to) = TvFeedRowWindow.InitialRange(5);

        from.Should().Be(4);
        to.Should().Be(7);
    }

    [Test]
    public void Grow_ShouldNeverShrinkVisitedRows()
    {
        var (from, to) = TvFeedRowWindow.Grow(0, 2, 5);

        from.Should().Be(0);
        to.Should().Be(7);
    }

    [Test]
    public void Grow_ShouldBeIdempotent_WhenAlreadyCovered()
    {
        var (from, to) = TvFeedRowWindow.Grow(0, 7, 3);

        from.Should().Be(0);
        to.Should().Be(7);
    }

    [Test]
    public void ShouldRenderContent_ShouldUseMountedRange()
    {
        TvFeedRowWindow.ShouldRenderContent(0, 0, 2).Should().BeTrue();
        TvFeedRowWindow.ShouldRenderContent(2, 0, 2).Should().BeTrue();
        TvFeedRowWindow.ShouldRenderContent(3, 0, 2).Should().BeFalse();
        TvFeedRowWindow.ShouldRenderContent(-1, 0, 2).Should().BeFalse();
    }
}
