using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Domain.Entities.Metadatas;
public class Person : BaseAuditableEntity
{
    public required string Name { get; set; }
    public PersonGender Gender { get; set; } = PersonGender.NotSpecified;
    public string? Biography { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Deathday { get; set; }
    public string? BirthPlace { get; set; }

    public IList<BasePersonRole> Roles { get; set; } = [];
    public IList<ExternalId> ExternalIds { get; set; } = [];
    public MetadataPicture? PortraitPicture { get; set; }
    // TODO - Rating is only associated to Medias right now, do we want to able able to rate persons?
    // public ICollection<BaseRating>? Ratings { get; set; }
}
