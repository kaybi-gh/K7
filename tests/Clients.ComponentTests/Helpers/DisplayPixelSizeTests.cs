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

    [Test]
    public void FromDisplay_ShouldKeepLogicalScreenAndPhysicalResolution_WhenLandscape1080pAt150Percent()
    {
        var (screenWidth, screenHeight, resolutionWidth, resolutionHeight) =
            DisplayPixelSize.FromDisplay(1280, 720, 1.5, landscape: true);

        screenWidth.Should().Be(1280);
        screenHeight.Should().Be(720);
        resolutionWidth.Should().Be(1920);
        resolutionHeight.Should().Be(1080);
    }

    [Test]
    public void FromDisplay_ShouldSwapPortraitAxesForScreenAndResolution()
    {
        var (screenWidth, screenHeight, resolutionWidth, resolutionHeight) =
            DisplayPixelSize.FromDisplay(360, 800, 3, landscape: false);

        screenWidth.Should().Be(800);
        screenHeight.Should().Be(360);
        resolutionWidth.Should().Be(2400);
        resolutionHeight.Should().Be(1080);
    }
}
