using K7.Clients.Shared.UI.Pages.MySpace;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MySpaceSharedBrowseHelperTests
{
    private static readonly Guid OwnedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SharedId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void BuildPlaylistItems_ShouldKeepOwnedOrder_WhenSharedIsEmpty()
    {
        var owned = new[]
        {
            Playlist("Zed", OwnedId),
            Playlist("Amy", SharedId)
        };

        var items = MySpaceSharedBrowseHelper.BuildPlaylistItems(
            owned, [], null, LibraryItemOrderingOption.LastListenedDesc);

        items.Select(i => i.Playlist.Title).Should().Equal("Zed", "Amy");
        items.Should().OnlyContain(i => i.IsOwned);
    }

    [Test]
    public void BuildPlaylistItems_ShouldAppendSharedWithoutReplacingOwned()
    {
        var owned = new[] { Playlist("Mine", OwnedId) };
        var shared = new[]
        {
            SharedPlaylist("Theirs", SharedId, "Alice")
        };

        var items = MySpaceSharedBrowseHelper.BuildPlaylistItems(
            owned, shared, null, LibraryItemOrderingOption.TitleAsc);

        items.Should().HaveCount(2);
        items.Should().ContainSingle(i => i.Id == OwnedId && i.IsOwned);
        var sharedItem = items.Should().ContainSingle(i => i.Id == SharedId).Subject;
        sharedItem.IsOwned.Should().BeFalse();
        sharedItem.Owner!.DisplayName.Should().Be("Alice");
    }

    [Test]
    public void BuildPlaylistItems_ShouldSkipSharedThatDuplicatesOwnedId()
    {
        var owned = new[] { Playlist("Mine", OwnedId) };
        var shared = new[]
        {
            SharedPlaylist("Also mine?", OwnedId, "Alice")
        };

        var items = MySpaceSharedBrowseHelper.BuildPlaylistItems(
            owned, shared, null, LibraryItemOrderingOption.TitleAsc);

        items.Should().ContainSingle();
        items[0].IsOwned.Should().BeTrue();
        items[0].Playlist.Title.Should().Be("Mine");
    }

    [Test]
    public void BuildPlaylistItems_ShouldFilterSharedByMediaType()
    {
        var owned = new[] { Playlist("Mine", OwnedId, MediaType.MusicTrack) };
        var shared = new[]
        {
            SharedPlaylist("Movie list", SharedId, "Alice", MediaType.Movie),
            SharedPlaylist("Tracks", Guid.Parse("33333333-3333-3333-3333-333333333333"), "Bob", MediaType.MusicTrack)
        };

        var items = MySpaceSharedBrowseHelper.BuildPlaylistItems(
            owned, shared, MediaType.MusicTrack, LibraryItemOrderingOption.TitleAsc);

        items.Select(i => i.Playlist.Title).Should().Equal("Mine", "Tracks");
    }

    [Test]
    public void BuildCollectionItems_ShouldAttachOwnerToExistingPublicCollection()
    {
        var current = new[] { Collection("Public mix", SharedId) };
        var shared = new[] { SharedCollection("Public mix", SharedId, "Alice") };

        var items = MySpaceSharedBrowseHelper.BuildCollectionItems(
            current, shared, LibraryItemOrderingOption.TitleAsc);

        items.Should().ContainSingle();
        items[0].IsOwned.Should().BeFalse();
        items[0].Owner!.DisplayName.Should().Be("Alice");
        items[0].Collection.Title.Should().Be("Public mix");
    }

    [Test]
    public void BuildCollectionItems_ShouldAppendMissingSharedCollections()
    {
        var current = new[] { Collection("Mine", OwnedId) };
        var shared = new[] { SharedCollection("Theirs", SharedId, "Bob") };

        var items = MySpaceSharedBrowseHelper.BuildCollectionItems(
            current, shared, LibraryItemOrderingOption.TitleAsc);

        items.Should().HaveCount(2);
        items.Should().Contain(i => i.Id == OwnedId && i.IsOwned);
        items.Should().Contain(i => i.Id == SharedId && !i.IsOwned && i.Owner!.DisplayName == "Bob");
    }

    [Test]
    public void FormatOwner_ShouldAppendPeerName_WhenFederated()
    {
        var owner = new SocialUserIdentityDto
        {
            IsFederated = true,
            DisplayName = "Alice",
            PeerName = "Peer A"
        };

        MySpaceSharedBrowseHelper.FormatOwner(owner).Should().Be("Alice @ Peer A");
    }

    [Test]
    public void FormatOwner_ShouldUseDisplayName_WhenLocal()
    {
        var owner = new SocialUserIdentityDto
        {
            IsFederated = false,
            DisplayName = "Alice"
        };

        MySpaceSharedBrowseHelper.FormatOwner(owner).Should().Be("Alice");
    }

    private static LitePlaylistDto Playlist(string title, Guid id, MediaType mediaType = MediaType.MusicTrack) => new()
    {
        Id = id,
        Title = title,
        MediaType = mediaType,
        Created = DateTimeOffset.UnixEpoch,
        LastModified = DateTimeOffset.UnixEpoch
    };

    private static SharedPlaylistBrowseDto SharedPlaylist(
        string title,
        Guid id,
        string owner,
        MediaType mediaType = MediaType.MusicTrack) => new()
        {
            Owner = new SocialUserIdentityDto { IsFederated = false, DisplayName = owner },
            PlaylistId = id,
            Title = title,
            MediaType = mediaType
        };

    private static LiteCollectionDto Collection(string title, Guid id) => new()
    {
        Id = id,
        Title = title,
        Created = DateTimeOffset.UnixEpoch,
        LastModified = DateTimeOffset.UnixEpoch
    };

    private static SharedCollectionBrowseDto SharedCollection(string title, Guid id, string owner) => new()
    {
        Owner = new SocialUserIdentityDto { IsFederated = false, DisplayName = owner },
        CollectionId = id,
        Title = title
    };
}
