using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Metadatas.PersonRoles;
public abstract class BasePersonRole(PersonRoleType type) : BaseAuditableEntity
{
    public PersonRoleType Type { get; protected set; } = type;
    public int? Order { get; set; }

    public IList<ExternalId> ExternalIds { get; set; } = [];
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;
    public MetadataPicture? PortraitPicture { get; set; }
}
