using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities;
public class MediaPicture : BaseAuditableEntity
{
    public required MediaPictureType Type { get; set; }
    public required string Path { get; set; }

    public required int MetadataId { get; set; }
    public virtual BaseMetadata? Metadata { get; set; }
}
