using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities;

public class ExternalId : BaseAuditableEntity
{
    public required string Platform { get; set; }
    public required string Value { get; set; }

    public required int MetadataId { get; set; }
    public virtual BaseMetadata? Metadata { get; set; }
}
