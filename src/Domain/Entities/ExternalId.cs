using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities;

public class ExternalId : BaseAuditableEntity
{
    public required int MediaId { get; set; }
    public required string Platform { get; set; }
    public required string Value { get; set; }

    public virtual required BaseMedia Media { get; set; }
}
