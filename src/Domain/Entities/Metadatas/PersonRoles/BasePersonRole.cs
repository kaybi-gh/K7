using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class BasePersonRole : BaseAuditableEntity
{
    public required string Name { get; set; }
    public PersonJob Job { get; set; }

    public required int PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public required int MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
    public virtual MetadataPicture? PortraitPicture { get; set; }
}
