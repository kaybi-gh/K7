using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Domain.Entities.Metadatas;
public class Person : BaseAuditableEntity
{
    public required string Name { get; set; }
    public PersonGender Gender { get; set; } = PersonGender.NotSpecified;
    public string? Biography { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Deathday { get; set; }
    public string? BirthPlace { get; set; }

    public virtual IEnumerable<BasePersonRole>? Roles { get; set; }
    public virtual ICollection<ExternalId>? ExternalIds { get; set; }
    public virtual MetadataPicture? PortraitPicture { get; set; }
    // TODO - Rating is only associated to Medias right now, do we want to able able to rate persons?
    // public virtual ICollection<BaseRating>? Ratings { get; set; }
}
