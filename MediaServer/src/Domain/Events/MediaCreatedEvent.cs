using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Events;

public class MediaCreatedEvent : BaseEvent
{
    public MediaCreatedEvent(BaseMedia media)
    {
        Media = media;
    }

    public BaseMedia Media { get; }
}
