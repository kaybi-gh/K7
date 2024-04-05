using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Domain.Entities;
public class MetadataPicture : BaseAuditableEntity
{
    public required MetadataPictureType Type { get; set; }
    public required string Path { get; set; }

    public int? MetadataId { get; set; }
    public virtual BaseMediaMetadata? Metadata { get; set; }
    public int? PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public int? PersonRoleId { get; set; }
    public virtual BasePersonRole? PersonRole { get; set; }

    public MetadataPicture()
    {
        if (MetadataId == null
            && PersonId == null
            && PersonRoleId == null)
        {
            throw new InvalidOperationException($"{nameof(MetadataPicture)} must have at least one foreign key.");
        }
    }
}
