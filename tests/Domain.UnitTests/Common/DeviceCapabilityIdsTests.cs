using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class DeviceCapabilityIdsTests
{
    [Test]
    public void Resolve_ShouldKeepExistingCatalog_WhenIncomingProbeIsEmpty()
    {
        var existing = new[] { "audio-mp4-aac", "video-mp4-aac-h264", "vprofile:hevc:main" };

        var resolved = DeviceCapabilityIds.Resolve([], existing);

        resolved.Should().Equal(existing);
    }

    [Test]
    public void Resolve_ShouldKeepExistingCatalog_WhenIncomingHasOnlyTokens()
    {
        var existing = new[] { "audio-mp4-aac", "video-mp4-aac-h264" };

        var resolved = DeviceCapabilityIds.Resolve(
            ["vprofile:hevc:main", "achannels:2"],
            existing);

        resolved.Should().Equal(existing);
    }

    [Test]
    public void Resolve_ShouldReplace_WhenIncomingHasCatalogFormat()
    {
        var resolved = DeviceCapabilityIds.Resolve(
            ["audio-mp4-aac", "video-mp4-aac-h264"],
            ["audio-mp3-mp3"]);

        resolved.Should().Equal("audio-mp4-aac", "video-mp4-aac-h264");
    }

    [Test]
    public void HasCatalogFormat_ShouldIgnoreProfileAndChannelTokens()
    {
        DeviceCapabilityIds.HasCatalogFormat(["vprofile:hevc:main10", "achannels:2"]).Should().BeFalse();
        DeviceCapabilityIds.HasCatalogFormat(["video-mp4-aac-h264"]).Should().BeTrue();
    }
}
