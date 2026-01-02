using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Domain.Events;

public class DeviceDeletedEvent : BaseEvent
{
    public DeviceDeletedEvent(Device device)
    {
        Device = device;
    }

    public Device Device { get; }
}
