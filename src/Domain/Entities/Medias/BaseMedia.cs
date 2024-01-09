namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia : BaseAuditableEntity
{
    public required int LibraryId { get; set; }
    public required MediaType Type { get; set; }
    public required Library MediaLibrary { get; set; }
    public virtual IEnumerable<IndexedFile> MediaFiles { get; set; } = [];

    //public virtual IList<BaseMetadata> Metadatas { get; set; } = null!;
}
