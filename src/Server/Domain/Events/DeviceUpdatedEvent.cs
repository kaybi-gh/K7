using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Domain.Events;

public class DeviceUpdatedEvent : BaseEvent
{
    public DeviceUpdatedEvent(Device device)
    {
        Device = device;
    }

    public Device Device { get; }
}
