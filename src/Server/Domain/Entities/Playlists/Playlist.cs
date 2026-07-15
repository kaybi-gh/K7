using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Domain.Entities.Playlists;

public class Playlist : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required MediaType MediaType { get; set; }
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Nobody;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public MetadataPicture? CoverPicture { get; set; }

    public IList<PlaylistItem> Items { get; set; } = [];

    public IList<UserPlaylistState> UserStates { get; set; } = [];

    public PlaylistItem AddItem(MediaType mediaType, Guid mediaId, int currentMaxOrder)
    {
        if (mediaType != MediaType)
            throw new InvalidOperationException($"Cannot add a media of type {mediaType} to a playlist of type {MediaType}.");

        var item = new PlaylistItem
        {
            PlaylistId = Id,
            MediaId = mediaId,
            Order = currentMaxOrder + 1
        };

        Items.Add(item);
        AddDomainEvent(new PlaylistItemAddedEvent(this, item));
        return item;
    }
}
