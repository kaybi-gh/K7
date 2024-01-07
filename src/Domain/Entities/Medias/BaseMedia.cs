using MediaServer.Domain.Entities.Files;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia : BaseAuditableEntity
{
    public required int MediaLibraryId { get; set; }
    public required MediaType Type { get; set; }
    public required Library MediaLibrary { get; set; }
    public virtual IList<Files.MediaFile> MediaFiles { get; set; } = new List<Files.MediaFile>();

    //public virtual IList<BaseMetadata> Metadatas { get; set; } = null!;
}
