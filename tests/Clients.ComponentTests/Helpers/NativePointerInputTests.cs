using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativePointerInputTests
{
    [Test]
    public void SupportsHoverRecognizers_ShouldBeFalse_WhenAndroid()
    {
        NativePointerInput.ForPlatform(isWindows: false, isAndroid: true, isIos: false)
            .Should().BeFalse();
    }

    [Test]
    public void SupportsHoverRecognizers_ShouldBeFalse_WhenIos()
    {
        NativePointerInput.ForPlatform(isWindows: false, isAndroid: false, isIos: true)
            .Should().BeFalse();
    }

    [Test]
    public void SupportsHoverRecognizers_ShouldBeFalse_WhenAndroidEvenIfWindowsFlagSet()
    {
        NativePointerInput.ForPlatform(isWindows: true, isAndroid: true, isIos: false)
            .Should().BeFalse();
    }

    [Test]
    public void SupportsHoverRecognizers_ShouldBeTrue_WhenWindowsDesktop()
    {
        NativePointerInput.ForPlatform(isWindows: true, isAndroid: false, isIos: false)
            .Should().BeTrue();
    }

    [Test]
    public void SupportsHoverRecognizers_ShouldBeFalse_WhenNeitherDesktopNorMobile()
    {
        NativePointerInput.ForPlatform(isWindows: false, isAndroid: false, isIos: false)
            .Should().BeFalse();
    }
}
