using K7.Clients.Shared.Helpers;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ConnectedDeviceLabelsTests
{
    [Test]
    public void GetDisplayName_ShouldPreferDeviceName()
    {
        var device = new ConnectedDeviceDto
        {
            DeviceId = Guid.NewGuid(),
            DeviceName = "  Living Room  ",
            DeviceType = "TV"
        };

        ConnectedDeviceLabels.GetDisplayName(device).Should().Be("Living Room");
    }

    [Test]
    public void GetDisplayName_ShouldFallBackToType_ThenGeneric()
    {
        ConnectedDeviceLabels.GetDisplayName(new ConnectedDeviceDto
            {
                DeviceId = Guid.NewGuid(),
                DeviceName = "   ",
                DeviceType = " Phone "
            })
            .Should().Be("Phone");
        ConnectedDeviceLabels.GetDisplayName(new ConnectedDeviceDto
            {
                DeviceId = Guid.NewGuid(),
                DeviceName = "",
                DeviceType = ""
            })
            .Should().Be("Device");
    }
}
