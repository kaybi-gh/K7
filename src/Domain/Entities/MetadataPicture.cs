using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Domain.Entities;
public class MetadataPicture : BaseAuditableEntity
{
    public required MetadataPictureType Type { get; set; }
    public required string Path { get; set; }

    public Guid? MetadataId { get; set; }
    public virtual BaseMediaMetadata? Metadata { get; set; }
    public Guid? PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public Guid? PersonRoleId { get; set; }
    public virtual BasePersonRole? PersonRole { get; set; }
}
