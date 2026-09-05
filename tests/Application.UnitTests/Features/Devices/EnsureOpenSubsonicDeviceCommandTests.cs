using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;

namespace K7.Server.Application.UnitTests.Features.Devices;

public class EnsureOpenSubsonicDeviceCommandTests
{
    [Test]
    public void NormalizeDeviceName_ShouldDefault_WhenMissing()
    {
        EnsureOpenSubsonicDeviceCommandHandler.NormalizeDeviceName(null).Should().Be("OpenSubsonic");
        EnsureOpenSubsonicDeviceCommandHandler.NormalizeDeviceName("  ").Should().Be("OpenSubsonic");
        EnsureOpenSubsonicDeviceCommandHandler.NormalizeDeviceName("Tempus").Should().Be("Tempus");
    }

    [Test]
    public void BuildUniqueId_ShouldBeStable_ForSameUserAndClient()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var a = EnsureOpenSubsonicDeviceCommandHandler.BuildUniqueId(userId, "Tempus");
        var b = EnsureOpenSubsonicDeviceCommandHandler.BuildUniqueId(userId, "tempus");
        a.Should().Be(b);
        a.Should().StartWith($"opensubsonic:{userId:D}:");
    }
}
