using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Helpers;

namespace K7.Server.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;

    public string? Title { get; set; }
    public string? SortTitle { get; set; }
    public string? OriginalTitle { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    public Guid? PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public IList<ExternalId> ExternalIds { get; set; } = [];
    public IList<MetadataPicture> Pictures { get; set; } = [];
    public IList<BaseRating> Ratings { get; set; } = [];
    public IList<BasePersonRole> PersonRoles { get; set; } = [];
    public IList<MediaMetadataTag> MetadataTags { get; set; } = [];
    public IList<TrailerInfo> Trailers { get; set; } = [];
    public IList<MediaRecommendation> Recommendations { get; set; } = [];
    public DateTimeOffset? LastMetadataRefreshedAt { get; set; }

    public IList<string> LockedFields { get; set; } = [];

    public IList<IndexedFile> IndexedFiles { get; set; } = [];
    public IList<RemoteIndexedFile> RemoteIndexedFiles { get; set; } = [];
    public IList<UserMediaState> UserMediaStates { get; set; } = [];
    public IList<MediaSegment> Segments { get; set; } = [];

    public bool IsFieldLocked(string fieldName) => LockedFields.Contains(fieldName);

    public bool IsPictureTypeLocked(MetadataPictureType type) =>
        MetadataPictureLockHelper.IsPictureTypeLocked(LockedFields, type);

    public void RemovePicturesOfType(MetadataPictureType type)
    {
        var existing = Pictures.Where(picture => picture.Type == type).ToList();
        foreach (var picture in existing)
            Pictures.Remove(picture);
    }

    protected void ApplyMetadataPictures(IEnumerable<MetadataPicture>? incomingPictures)
    {
        if (incomingPictures is null)
            return;

        foreach (var picture in incomingPictures)
        {
            if (IsPictureTypeLocked(picture.Type))
                continue;

            RemovePicturesOfType(picture.Type);
            Pictures.Add(picture);
        }
    }

    public bool IsRemoteOrigin => PeerServerId is not null;
}
