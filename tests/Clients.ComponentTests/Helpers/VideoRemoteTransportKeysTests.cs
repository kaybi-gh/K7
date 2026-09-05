using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class VideoRemoteTransportKeysTests
{
    [Test]
    public void IsAndroidSkip_ShouldDetectFastForwardAndRewind_NotDpad()
    {
        VideoRemoteTransportKeys.IsAndroidSkipForward(90).Should().BeTrue();
        VideoRemoteTransportKeys.IsAndroidSkipBack(89).Should().BeTrue();
        VideoRemoteTransportKeys.IsAndroidSkip(272).Should().BeTrue();
        VideoRemoteTransportKeys.IsAndroidSkip(273).Should().BeTrue();
        VideoRemoteTransportKeys.IsAndroidSkip(22).Should().BeFalse();
        VideoRemoteTransportKeys.IsAndroidSkip(21).Should().BeFalse();
    }

    [Test]
    public void OverlayKey_ShouldBeDistinctFromDpadSoChromeFocusIsNotStolen()
    {
        VideoRemoteTransportKeys.OverlayKey(forward: true).Should().Be("mediafastforward");
        VideoRemoteTransportKeys.OverlayKey(forward: false).Should().Be("mediarewind");
        VideoRemoteTransportKeys.IsOverlaySkip("dpad_right").Should().BeFalse();
        VideoRemoteTransportKeys.IsOverlaySkipForward("mediafastforward").Should().BeTrue();
        VideoRemoteTransportKeys.IsOverlaySkipBack("mediarewind").Should().BeTrue();
    }
}
