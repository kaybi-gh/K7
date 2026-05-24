using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Events;

public class MediaMetadataRefreshedEvent(BaseMedia media) : BaseEvent
{
    public BaseMedia Media { get; } = media;
}
