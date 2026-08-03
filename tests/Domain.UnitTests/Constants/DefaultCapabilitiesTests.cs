using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.UnitTests.Constants;

[TestFixture]
public class DefaultCapabilitiesTests
{
    [Test]
    public void ForRole_ShouldAllowGuestToReportPlaybackProgressOnly()
    {
        var caps = DefaultCapabilities.ForRole(Roles.Guest);

        caps.Should().BeEquivalentTo([Capability.CanReportPlaybackProgress]);
    }

    [Test]
    public void ForRole_ShouldKeepPersonalProgressCapabilitiesForUser()
    {
        var caps = DefaultCapabilities.ForRole(Roles.User);

        caps.Should().Contain(Capability.CanReportPlaybackProgress);
        caps.Should().Contain(Capability.CanResumePlayback);
        caps.Should().Contain(Capability.CanViewHistory);
        caps.Should().Contain(Capability.CanViewStats);
    }
}
