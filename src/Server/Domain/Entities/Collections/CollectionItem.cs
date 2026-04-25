using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Collections;

public class CollectionItem : BaseEntity
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public int Order { get; set; }
}
