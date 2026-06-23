using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Metadatas;

public class MediaMetadataTag
{
    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public Guid MetadataTagId { get; set; }
    public MetadataTag MetadataTag { get; set; } = null!;
}
