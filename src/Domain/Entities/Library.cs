using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities;

public class Library : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public required string RootPath { get; set; }
    public bool? RootPathAccessible { get; set; }

    public virtual ICollection<IndexedFile>? IndexedFiles { get; set; }
    public virtual ICollection<BaseMedia>? Medias { get; set; }
}
