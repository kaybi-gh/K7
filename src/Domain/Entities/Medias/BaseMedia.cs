using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia : BaseAuditableEntity
{
    protected BaseMedia(MediaType type)
    {
        Type = type;
    }

    public MediaType Type { get; protected set; }

    public required int LibraryId { get; set; }
    public virtual required Library Library { get; set; }
    public virtual ICollection<IndexedFile> IndexedFiles { get; set; } = [];
    public virtual ICollection<BaseRating> Ratings { get; set; } = [];
}
