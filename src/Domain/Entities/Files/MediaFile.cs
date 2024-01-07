
namespace MediaServer.Domain.Entities.Files;
public class MediaFile : BaseAuditableEntity
{
    public required int LibraryId { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required string Path { get; set; }
    public string? ParentDirectory { get; set; }
    public required string Hash { get; set; }
    public required long Size { get; set; }
    public bool IsIdentified { get; set; } = false;
}
