using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Domain.Events;

public class DeviceCreatedEvent : BaseEvent
{
    public DeviceCreatedEvent(Device device)
    {
        Device = device;
    }

    public Device Device { get; }
}
