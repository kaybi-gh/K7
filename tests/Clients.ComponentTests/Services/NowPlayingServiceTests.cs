using AwesomeAssertions;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public sealed class NowPlayingServiceTests
{
    [Test]
    public void FilterOtherDevices_ShouldDropCurrentDevice()
    {
        var self = Guid.NewGuid();
        var other = Guid.NewGuid();
        var sessions = new[]
        {
            new NowPlayingSessionDto { SessionId = Guid.NewGuid(), DeviceId = self, MediaTitle = "A" },
            new NowPlayingSessionDto { SessionId = Guid.NewGuid(), DeviceId = other, MediaTitle = "B" },
            new NowPlayingSessionDto { SessionId = Guid.NewGuid(), DeviceId = null, MediaTitle = "C" }
        };

        var filtered = NowPlayingService.FilterOtherDevices(sessions, self);

        filtered.Should().HaveCount(2);
        filtered.Select(s => s.MediaTitle).Should().BeEquivalentTo("B", "C");
    }

    [Test]
    public void FilterOtherDevices_ShouldKeepAll_WhenSelfUnknown()
    {
        var sessions = new[]
        {
            new NowPlayingSessionDto { SessionId = Guid.NewGuid(), DeviceId = Guid.NewGuid() }
        };

        NowPlayingService.FilterOtherDevices(sessions, null).Should().HaveCount(1);
    }
}
