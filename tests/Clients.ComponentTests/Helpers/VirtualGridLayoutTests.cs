using K7.Clients.Shared.UI.Components;
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
    public void CalculateColumnCount_ShouldKeepTwoCompactColumns_WhenStillMatchesPosterHeight()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(
            328, MediaCardLayout.GridItemWidth(MediaCardVariant.Backdrop), 24, 9f / 16f);

        cols.Should().Be(2);
    }

    [Test]
    public void CalculateColumnCount_ShouldCapStillsAtTwoColumns_OnCompactWidePhones()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(520, 427, 24, 9f / 16f);

        cols.Should().Be(2);
    }

    [Test]
    public void CalculateColumnCount_ShouldKeepTwoStillColumns_OnNarrowDesktop()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(720, 427, 24, 9f / 16f);

        cols.Should().Be(2);
    }

    [Test]
    public void CalculateColumnCount_ShouldUseDesktopFloor_OnWideContainers()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(1200, 160, 24, 1.5f);

        cols.Should().Be(6);
    }

    [Test]
    public void CalculateColumnCount_ShouldCapAtEight_OnWideTvPosterGrid()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(1920, 160, 24, 1.5f, 8);

        cols.Should().Be(8);
    }

    [Test]
    public void CalculateColumnCount_ShouldAllowTen_OnWideDesktopWithoutCap()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(1920, 160, 24, 1.5f);

        cols.Should().Be(10);
    }
}
