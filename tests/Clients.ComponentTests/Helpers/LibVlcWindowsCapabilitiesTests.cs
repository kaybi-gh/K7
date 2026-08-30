using FluentAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class LibVlcWindowsCapabilitiesTests
{
    [Test]
    public void VideoCodecs_ShouldIncludeHevcAndH264()
    {
        LibVlcWindowsCapabilities.VideoCodecs.Should().Contain(["h264", "hevc", "av1"]);
    }

    [Test]
    public void AudioCodecs_ShouldIncludeEac3AndDts()
    {
        LibVlcWindowsCapabilities.AudioCodecs.Should().Contain(["aac", "eac3", "ac3", "dts", "truehd"]);
    }

    [Test]
    public void GetContainers_ShouldIncludeMatroska()
    {
        LibVlcWindowsCapabilities.GetContainers().Should().Contain("matroska");
    }

    [Test]
    public void GetContainers_ShouldAdvertiseMatroska_WhenOnlyHevcIsPresent()
    {
        LibVlcWindowsCapabilities.GetContainers(["hevc"]).Should().Contain("matroska");
    }
}
