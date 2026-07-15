using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Domain.UnitTests.Entities.Playlists;

public class PlaylistTests
{
    [Test]
    public void AddItem_ShouldAssignOrderAndRaiseEvent_WhenMediaTypeMatches()
    {
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            MediaType = MediaType.Movie,
            UserId = Guid.NewGuid()
        };

        var item = playlist.AddItem(MediaType.Movie, Guid.NewGuid(), 2);

        Assert.That(item.Order, Is.EqualTo(3));
        Assert.That(playlist.Items, Contains.Item(item));
        Assert.That(playlist.DomainEvents, Has.One.InstanceOf<PlaylistItemAddedEvent>());
    }

    [Test]
    public void AddItem_ShouldThrow_WhenMediaTypeDoesNotMatch()
    {
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            MediaType = MediaType.Movie,
            UserId = Guid.NewGuid()
        };

        Assert.Throws<InvalidOperationException>(() => playlist.AddItem(MediaType.MusicTrack, Guid.NewGuid(), 0));
    }
}
