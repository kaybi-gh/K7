using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class CarouselVirtualWindowTests
{
    [Test]
    public void FromVisibleRange_ShouldReturnEmpty_WhenNoItems()
    {
        var (first, last) = CarouselVirtualWindow.FromVisibleRange(0, 3, 4, 0);

        first.Should().Be(0);
        last.Should().Be(-1);
    }

    [Test]
    public void FromVisibleRange_ShouldIncludeOverscan_AndClampToCount()
    {
        var (first, last) = CarouselVirtualWindow.FromVisibleRange(2, 6, 4, 20);

        first.Should().Be(0);
        last.Should().Be(10);
    }

    [Test]
    public void FromVisibleRange_ShouldNotGoPastLastItem()
    {
        var (first, last) = CarouselVirtualWindow.FromVisibleRange(16, 19, 4, 20);

        first.Should().Be(12);
        last.Should().Be(19);
    }

    [Test]
    public void FromAnchor_ShouldOpenAWindowAroundTheCard()
    {
        var (first, last) = CarouselVirtualWindow.FromAnchor(8, 4, 20);

        first.Should().Be(4);
        last.Should().Be(19);
    }

    [Test]
    public void Contains_ShouldBeInclusive()
    {
        CarouselVirtualWindow.Contains(2, 5, 2).Should().BeTrue();
        CarouselVirtualWindow.Contains(2, 5, 5).Should().BeTrue();
        CarouselVirtualWindow.Contains(2, 5, 1).Should().BeFalse();
        CarouselVirtualWindow.Contains(2, 5, 6).Should().BeFalse();
    }
}
