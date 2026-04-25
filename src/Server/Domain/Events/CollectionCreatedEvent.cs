using K7.Server.Domain.Entities.Collections;

namespace K7.Server.Domain.Events;

public class CollectionCreatedEvent(Collection collection) : BaseEvent
{
    public Collection Collection { get; } = collection;
}
