using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Domain.Entities;
public class MetadataPicture : BaseAuditableEntity
{
    public required MetadataPictureType Type { get; set; }
    public Uri? OriginalRemoteUri { get; set; }
    public string? LocalPath { get; set; }
    public string? DominantColor { get; set; }

    public Guid? MediaId { get; set; }
    public BaseMedia? Media { get; set; }
    public Guid? VideoFileMetadataId { get; set; }
    public VideoFileMetadata? VideoFileMetadata { get; set; }
    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }
    public Guid? PersonRoleId { get; set; }
    public BasePersonRole? PersonRole { get; set; }
    public Guid? PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }
    public Guid? LibraryId { get; set; }
    public Library? Library { get; set; }

    public IList<MetadataPictureVariant> Variants { get; set; } = [];
}
