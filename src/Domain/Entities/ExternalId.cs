using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities;

public class ExternalId : BaseAuditableEntity
{
    public required string Platform { get; set; }
    public required string Value { get; set; }

    public required int MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
}
