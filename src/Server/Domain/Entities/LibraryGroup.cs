namespace K7.Server.Domain.Entities;

public class LibraryGroup : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? CardColor { get; set; }

    public MetadataPicture? CoverPicture { get; set; }
    public IList<Library> Libraries { get; set; } = [];
}
