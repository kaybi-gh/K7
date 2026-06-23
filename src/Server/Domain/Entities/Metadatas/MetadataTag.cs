using K7.Server.Domain.Common;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Metadatas;

public class MetadataTag : BaseEntity
{
    public MetadataTagKind Kind { get; set; }
    public required string NormalizedKey { get; set; }
    public required string DisplayName { get; set; }

    public IList<MediaMetadataTag> MediaAssignments { get; set; } = [];
}
