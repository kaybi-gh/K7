using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DisplayPixelSizeTests
{
    [Test]
    public void FromDip_ShouldScaleLandscape1080pAt150Percent()
    {
        var (width, height) = DisplayPixelSize.FromDip(1280, 720, 1.5, landscape: true);

        width.Should().Be(1920);
        height.Should().Be(1080);
    }

    [Test]
    public void FromDip_ShouldSwapPortraitToLandscapeAxes()
    {
        var (width, height) = DisplayPixelSize.FromDip(360, 800, 3, landscape: false);

        width.Should().Be(2400);
        height.Should().Be(1080);
    }
}
