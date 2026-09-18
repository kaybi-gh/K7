using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AudioPlayerSourceTrackTests
{
    [Test]
    public void Resolve_ShouldReturnNextQueueItem_WhenPrebufferSourcePointsAtIndexedFile()
    {
        var current = Track("now");
        var next = Track("next");
        var source = new PlayerSource
        {
            IndexedFileId = next.IndexedFileId,
            Url = "https://k7.example/next"
        };

        var resolved = AudioPlayerSourceTrack.Resolve([current, next], source, current, current);

        resolved.Should().BeSameAs(next);
    }

    [Test]
    public void Resolve_ShouldPreferMediaId_WhenQueueContainsBothIds()
    {
        var current = Track("now");
        var next = Track("next");
        var source = new PlayerSource
        {
            MediaId = next.MediaId,
            IndexedFileId = current.IndexedFileId
        };

        var resolved = AudioPlayerSourceTrack.Resolve([current, next], source, current, current);

        resolved.Should().BeSameAs(next);
    }

    [Test]
    public void Resolve_ShouldFallBackToCurrentTrack_WhenSourceHasNoQueueMatch()
    {
        var current = Track("now");
        var source = new PlayerSource { Url = "https://k7.example/unknown" };

        var resolved = AudioPlayerSourceTrack.Resolve([current], source, current, null);

        resolved.Should().BeSameAs(current);
    }

    private static AudioQueueItem Track(string title) => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = title,
        Artist = "Artist",
        AlbumTitle = "Album"
    };
}
