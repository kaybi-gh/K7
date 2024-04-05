using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;

    public virtual BaseMediaMetadata? Metadata { get; set; }
    public virtual IEnumerable<BasePersonRole>? PersonRoles { get; set; }
    public virtual IList<IndexedFile>? IndexedFiles { get; set; }
}
