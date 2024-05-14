using MediaServer.Domain.Entities.Metadatas.Medias;

namespace MediaServer.Domain.Entities.Metadatas.Persons;
public abstract class BasePersonRole(PersonRoleType type) : BaseAuditableEntity
{
    public PersonRoleType Type { get; protected set; } = type;
    public int? Order {  get; set; }

    public IList<ExternalId> ExternalIds { get; set; } = [];
    public Guid PersonId { get; set; }
    public virtual Person Person { get; set; } = null!;
    public Guid MetadataId { get; set; }
    public virtual BaseMediaMetadata Metadata { get; set; } = null!;
    public virtual MetadataPicture? PortraitPicture { get; set; }
}
