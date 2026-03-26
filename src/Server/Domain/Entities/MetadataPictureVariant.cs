namespace K7.Server.Domain.Entities;

public class MetadataPictureVariant : BaseAuditableEntity
{
    public required MetadataPictureSize Size { get; set; }
    public required string LocalPath { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Guid MetadataPictureId { get; set; }
    public MetadataPicture MetadataPicture { get; set; } = null!;
}
