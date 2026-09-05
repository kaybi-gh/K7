using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.Shared.UI.Pages.MySpace;

internal sealed record MySpacePlaylistBrowseItem(LitePlaylistDto Playlist, SocialUserIdentityDto? Owner)
{
    public Guid Id => Playlist.Id;
    public bool IsOwned => Owner is null;
}

internal sealed record MySpaceCollectionBrowseItem(LiteCollectionDto Collection, SocialUserIdentityDto? Owner)
{
    public Guid Id => Collection.Id;
    public bool IsOwned => Owner is null;
}

internal static class MySpaceSharedBrowseHelper
{
    internal static IReadOnlyList<MySpacePlaylistBrowseItem> BuildPlaylistItems(
        IReadOnlyList<LitePlaylistDto> owned,
        IReadOnlyList<SharedPlaylistBrowseDto>? shared,
        MediaType? mediaTypeFilter,
        LibraryItemOrderingOption sort)
    {
        var items = owned.Select(playlist => new MySpacePlaylistBrowseItem(playlist, null)).ToList();
        if (shared is null || shared.Count == 0)
            return items;

        var byId = items.ToDictionary(item => item.Id);
        foreach (var sharedPlaylist in shared)
        {
            if (mediaTypeFilter is MediaType type && sharedPlaylist.MediaType is MediaType sharedType && sharedType != type)
                continue;

            if (byId.ContainsKey(sharedPlaylist.PlaylistId))
                continue;

            byId[sharedPlaylist.PlaylistId] = new MySpacePlaylistBrowseItem(
                ToLitePlaylist(sharedPlaylist),
                sharedPlaylist.Owner);
        }

        return SortPlaylists(byId.Values, sort);
    }

    internal static IReadOnlyList<MySpaceCollectionBrowseItem> BuildCollectionItems(
        IReadOnlyList<LiteCollectionDto> current,
        IReadOnlyList<SharedCollectionBrowseDto>? shared,
        LibraryItemOrderingOption sort)
    {
        var items = current.Select(collection => new MySpaceCollectionBrowseItem(collection, null)).ToList();
        if (shared is null || shared.Count == 0)
            return items;

        var byId = items.ToDictionary(item => item.Id);
        foreach (var sharedCollection in shared)
        {
            if (byId.TryGetValue(sharedCollection.CollectionId, out var existing))
            {
                byId[sharedCollection.CollectionId] = existing with { Owner = sharedCollection.Owner };
                continue;
            }

            byId[sharedCollection.CollectionId] = new MySpaceCollectionBrowseItem(
                ToLiteCollection(sharedCollection),
                sharedCollection.Owner);
        }

        return SortCollections(byId.Values, sort);
    }

    internal static string FormatOwner(SocialUserIdentityDto owner)
    {
        if (!string.IsNullOrWhiteSpace(owner.PeerName))
            return $"{owner.DisplayName} @ {owner.PeerName}";

        return owner.DisplayName;
    }

    internal static LitePlaylistDto ToLitePlaylist(SharedPlaylistBrowseDto shared) => new()
    {
        Id = shared.PlaylistId,
        Title = shared.Title,
        Description = shared.Description,
        IsDynamicPlaylist = shared.IsDynamic,
        MediaType = shared.MediaType ?? MediaType.MusicTrack,
        CoverPicture = shared.CoverPicture,
        PreviewPictures = shared.PreviewPictures,
        ItemCount = shared.ItemCount,
        Created = shared.Created,
        LastModified = shared.LastModified
    };

    internal static LiteCollectionDto ToLiteCollection(SharedCollectionBrowseDto shared) => new()
    {
        Id = shared.CollectionId,
        Title = shared.Title,
        Description = shared.Description,
        UserId = shared.Owner.LocalUserId,
        MediaType = shared.MediaType,
        IsPublic = shared.IsPublic,
        CoverPicture = shared.CoverPicture,
        PreviewPictures = shared.PreviewPictures,
        ItemCount = shared.ItemCount,
        Created = shared.Created,
        LastModified = shared.LastModified
    };

    private static IReadOnlyList<MySpacePlaylistBrowseItem> SortPlaylists(
        IEnumerable<MySpacePlaylistBrowseItem> items,
        LibraryItemOrderingOption sort)
    {
        IOrderedEnumerable<MySpacePlaylistBrowseItem> ordered = sort switch
        {
            LibraryItemOrderingOption.TitleAsc => items.OrderBy(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(i => i.Playlist.LastModified),
            LibraryItemOrderingOption.TitleDesc => items.OrderByDescending(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(i => i.Playlist.LastModified),
            LibraryItemOrderingOption.CreatedAsc => items.OrderBy(i => i.Playlist.Created)
                .ThenBy(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase),
            LibraryItemOrderingOption.CreatedDesc => items.OrderByDescending(i => i.Playlist.Created)
                .ThenBy(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase),
            LibraryItemOrderingOption.LastListenedAsc => items.OrderBy(i => i.Playlist.LastListenedAt ?? DateTimeOffset.MaxValue)
                .ThenBy(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderByDescending(i => i.Playlist.LastListenedAt ?? DateTimeOffset.MinValue)
                .ThenBy(i => i.Playlist.Title, StringComparer.OrdinalIgnoreCase)
        };

        return ordered.ToList();
    }

    private static IReadOnlyList<MySpaceCollectionBrowseItem> SortCollections(
        IEnumerable<MySpaceCollectionBrowseItem> items,
        LibraryItemOrderingOption sort)
    {
        IOrderedEnumerable<MySpaceCollectionBrowseItem> ordered = sort switch
        {
            LibraryItemOrderingOption.TitleAsc => items.OrderBy(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(i => i.Collection.LastModified),
            LibraryItemOrderingOption.TitleDesc => items.OrderByDescending(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(i => i.Collection.LastModified),
            LibraryItemOrderingOption.CreatedAsc => items.OrderBy(i => i.Collection.Created)
                .ThenBy(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase),
            LibraryItemOrderingOption.CreatedDesc => items.OrderByDescending(i => i.Collection.Created)
                .ThenBy(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase),
            LibraryItemOrderingOption.LastModifiedAsc => items.OrderBy(i => i.Collection.LastModified)
                .ThenBy(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderByDescending(i => i.Collection.LastModified)
                .ThenBy(i => i.Collection.Title, StringComparer.OrdinalIgnoreCase)
        };

        return ordered.ToList();
    }
}
