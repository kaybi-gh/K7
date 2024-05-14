using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Entities.Ratings;

namespace MediaServer.Domain.Entities.Metadatas.Medias;
public abstract class BaseMediaMetadata(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;

    public required string Title { get; set; }
    public required string OriginalTitle { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    public Guid MediaId { get; set; }
    public virtual BaseMedia Media { get; set; } = null!;
    public virtual IList<ExternalId> ExternalIds { get; set; } = [];
    public virtual IList<MetadataPicture> Pictures { get; set; } = [];
    public virtual IList<BaseRating> Ratings { get; set; } = [];
    public virtual IList<BasePersonRole> PersonRoles { get; set; } = [];
    public virtual IList<string> Genres { get; set; } = [];
}
