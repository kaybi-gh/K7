using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Domain.Entities;

public class ExternalId : BaseAuditableEntity
{
    public required string Platform { get; set; }
    public required string Value { get; set; }

    public Guid? MetadataId { get; set; }
    public virtual BaseMediaMetadata? Metadata { get; set; }
    public Guid? PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public Guid? PersonRoleId { get; set; }
    public virtual BasePersonRole? PersonRole { get; set; }
}
