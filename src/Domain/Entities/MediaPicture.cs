namespace MediaServer.Domain.Entities;
public class MediaPicture : BaseAuditableEntity
{
    public required int MediaId { get; set; }
    public required MediaPictureType Type { get; set; }
    public required string Path { get; set; }
}
