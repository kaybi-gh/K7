using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class NativeOverlayHoverTests
{
    [Test]
    public void SupportsHoverRecognizers_ShouldBeTrue_OnWindowsHost()
    {
        NativePointerInput.SupportsHoverRecognizers.Should().BeTrue();
    }
}
