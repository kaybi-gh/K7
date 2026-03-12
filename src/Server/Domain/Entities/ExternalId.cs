using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Domain.Entities;

public class ExternalId : BaseAuditableEntity
{
    public required string ProviderName { get; set; }
    public required string Value { get; set; }

    public Guid? MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
    public Guid? PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public Guid? PersonRoleId { get; set; }
    public virtual BasePersonRole? PersonRole { get; set; }
}
