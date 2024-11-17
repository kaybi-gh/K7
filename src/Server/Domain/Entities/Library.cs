namespace K7.Server.Domain.Entities;

public class Library : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public required string RootPath { get; set; }
    public bool? RootPathAccessible { get; set; }

    public virtual IList<IndexedFile> IndexedFiles { get; set; } = [];
}
