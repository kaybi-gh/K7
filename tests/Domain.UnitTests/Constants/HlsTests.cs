using K7.Server.Domain.Constants;

namespace K7.Server.Domain.UnitTests.Constants;

[TestFixture]
public class HlsTests
{
    [Test]
    public void VideoTfdtRebaseToleranceMs_ShouldUseTightAlign_WhenEncode()
    {
        Hls.VideoTfdtRebaseToleranceMs(isEncode: true).Should().Be(Hls.VideoTfdtAlignToleranceMs);
    }

    [Test]
    public void VideoTfdtRebaseToleranceMs_ShouldKeepWindowReset_WhenCopy()
    {
        Hls.VideoTfdtRebaseToleranceMs(isEncode: false).Should().Be(Hls.TfdtWindowResetThresholdMs);
    }
}
