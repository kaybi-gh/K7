namespace MediaServer.Domain.Events;

public class MetadataPictureCreatedEvent : BaseEvent
{
    public MetadataPictureCreatedEvent(MetadataPicture metadataPicture)
    {
        MetadataPicture = metadataPicture;
    }

    public MetadataPicture MetadataPicture { get; }
}
