using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class VirtualGridLayoutTests
{
    [Test]
    public void CalculateColumnCount_ShouldReturnTwoColumns_OnCompactPosterGridAt390()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(328, 160, 24, 1.5f);

        cols.Should().Be(2);
    }

    [Test]
    public void CalculateColumnCount_ShouldReturnThreeColumns_OnCompactPosterGridAt412()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(350, 160, 24, 1.5f);

        cols.Should().Be(3);
    }

    [Test]
    public void CalculateColumnCount_ShouldReturnTwoColumns_OnCompactBackdropGridAt390()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(328, 200, 24, 9f / 16f);

        cols.Should().Be(2);
    }

    [Test]
    public void CalculateColumnCount_ShouldUseDesktopFloor_OnWideContainers()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(1200, 160, 24, 1.5f);

        cols.Should().Be(6);
    }
}
