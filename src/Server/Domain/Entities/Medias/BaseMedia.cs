using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type) : BaseSlugEntity
{
    public MediaType Type { get; protected set; } = type;

    public virtual BaseMediaMetadata? Metadata { get; set; }
    public virtual IList<IndexedFile> IndexedFiles { get; set; } = [];
}
