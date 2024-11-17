using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Events;

public class MediaCreatedEvent : BaseEvent
{
    public MediaCreatedEvent(BaseMedia media)
    {
        Media = media;
    }

    public BaseMedia Media { get; }
}
