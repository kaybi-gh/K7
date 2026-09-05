using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TvVerticalWindowTests
{
    [Test]
    public void GrowTo_ShouldNotRequestARender_WhenRangeAlreadyCoversTheRow()
    {
        var window = new TvVerticalWindow();
        window.Reset(0);

        window.GrowTo(0).Should().BeFalse();
        window.MountedFrom.Should().Be(0);
        window.MountedTo.Should().Be(2);
    }

    [Test]
    public void GrowTo_ShouldRequestARender_OnlyWhenTheRangeGrows()
    {
        var window = new TvVerticalWindow();
        window.Reset(0);

        window.GrowTo(1).Should().BeTrue();
        window.MountedTo.Should().Be(3);
        window.GrowTo(1).Should().BeFalse();
        window.GrowTo(3).Should().BeTrue();
        window.MountedFrom.Should().Be(0);
        window.MountedTo.Should().Be(5);
    }
}
