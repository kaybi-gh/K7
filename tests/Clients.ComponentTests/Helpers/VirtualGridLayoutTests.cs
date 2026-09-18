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

    [Test]
    public void IsCompact_ShouldBeTrue_OnShortViewport()
    {
        VirtualGridLayout.IsCompact(844, 280).Should().BeTrue();
        VirtualGridLayout.IsCompact(844, 800).Should().BeFalse();
        VirtualGridLayout.IsCompact(390, 800).Should().BeTrue();
    }

    [Test]
    public void GetEffectiveSpacing_ShouldUseCompactSpacing_OnShortViewport()
    {
        VirtualGridLayout.GetEffectiveSpacing(844, 24, 280).Should().Be(VirtualGridLayout.CompactSpacing);
        VirtualGridLayout.GetEffectiveSpacing(844, 24, 800).Should().Be(24);
    }

    [Test]
    public void CalculateColumnCount_ShouldAddColumns_WhenShortLandscapeRowWouldOverflow()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(800, 160, 24, 1.5f, containerHeight: 220);

        cols.Should().BeGreaterThan(4);
    }

    [Test]
    public void CalculateColumnCount_ShouldKeepDesktopColumns_WhenViewportIsTall()
    {
        var cols = VirtualGridLayout.CalculateColumnCount(800, 160, 24, 1.5f, containerHeight: 800);

        cols.Should().Be(4);
    }
}
