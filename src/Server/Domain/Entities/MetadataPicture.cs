using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Domain.Entities;
public class MetadataPicture : BaseAuditableEntity
{
    public required MetadataPictureType Type { get; set; }
    public Uri? OriginalRemoteUri { get; set; }
    public string? LocalPath { get; set; }

    public Guid? MediaId { get; set; }
    public virtual BaseMedia? Media { get; set; }
    public Guid? VideoFileMetadataId { get; set; }
    public virtual VideoFileMetadata? VideoFileMetadata { get; set; }
    public Guid? PersonId { get; set; }
    public virtual Person? Person { get; set; }
    public Guid? PersonRoleId { get; set; }
    public virtual BasePersonRole? PersonRole { get; set; }

    public virtual ICollection<MetadataPictureVariant> Variants { get; set; } = [];
}
