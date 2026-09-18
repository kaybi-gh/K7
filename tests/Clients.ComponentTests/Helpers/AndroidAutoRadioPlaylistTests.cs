using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AndroidAutoRadioPlaylistTests
{
    [Test]
    public void UpcomingNotOnPlayer_ShouldSkipCurrentAndAlreadyLoaded()
    {
        var queue = Tracks(3);
        var onPlayer = new HashSet<Guid> { queue[0].MediaId };

        var missing = AndroidAutoRadioPlaylist.UpcomingNotOnPlayer(queue, currentIndex: 0, onPlayer);

        missing.Select(t => t.MediaId).Should().Equal(queue[1].MediaId, queue[2].MediaId);
    }

    [Test]
    public void UpcomingNotOnPlayer_ShouldIgnoreTracksBeforeCurrentIndex()
    {
        var queue = Tracks(4);
        var onPlayer = new HashSet<Guid> { queue[2].MediaId };

        var missing = AndroidAutoRadioPlaylist.UpcomingNotOnPlayer(queue, currentIndex: 2, onPlayer);

        missing.Select(t => t.MediaId).Should().Equal(queue[3].MediaId);
    }

    [Test]
    public void UpcomingNotOnPlayer_ShouldReturnEmpty_WhenPlayerAlreadyHasUpcoming()
    {
        var queue = Tracks(2);
        var onPlayer = new HashSet<Guid> { queue[0].MediaId, queue[1].MediaId };

        AndroidAutoRadioPlaylist.UpcomingNotOnPlayer(queue, currentIndex: 0, onPlayer)
            .Should().BeEmpty();
    }

    [Test]
    public void UpcomingNotOnPlayer_ShouldTreatStaleIdsAsMissing_WhenPlayerWasReset()
    {
        var queue = Tracks(3);
        var onPlayer = AndroidAutoRadioPlaylist.ParsePlayerMediaIds([queue[0].MediaId.ToString()]);

        var missing = AndroidAutoRadioPlaylist.UpcomingNotOnPlayer(queue, currentIndex: 0, onPlayer);

        missing.Select(t => t.MediaId).Should().Equal(queue[1].MediaId, queue[2].MediaId);
    }

    [Test]
    public void MissingFromPlayer_ShouldPrependPlayedTracks()
    {
        var queue = Tracks(4);
        var onPlayer = new HashSet<Guid> { queue[2].MediaId };

        var gap = AndroidAutoRadioPlaylist.MissingFromPlayer(queue, currentIndex: 2, onPlayer);

        gap.ToPrepend.Select(t => t.MediaId).Should().Equal(queue[0].MediaId, queue[1].MediaId);
        gap.ToAppend.Select(t => t.MediaId).Should().Equal(queue[3].MediaId);
    }

    [Test]
    public void ShouldRoutePreviousThroughQueue_ShouldBeTrue_WhenNativeIndexIsFirstButQueueHasPast()
    {
        AndroidAutoRadioPlaylist.ShouldRoutePreviousThroughQueue(nativeIndex: 0, hasPreviousInQueue: true)
            .Should().BeTrue();
        AndroidAutoRadioPlaylist.ShouldRoutePreviousThroughQueue(nativeIndex: 2, hasPreviousInQueue: true)
            .Should().BeFalse();
        AndroidAutoRadioPlaylist.ShouldRoutePreviousThroughQueue(nativeIndex: 0, hasPreviousInQueue: false)
            .Should().BeFalse();
    }

    [Test]
    public void ParsePlayerMediaIds_ShouldIgnoreNonGuidIds()
    {
        var id = Guid.NewGuid();

        AndroidAutoRadioPlaylist.ParsePlayerMediaIds(["radio:Discovery", id.ToString(), null, "nope"])
            .Should().BeEquivalentTo([id]);
    }

    private static List<AudioQueueItem> Tracks(int count)
    {
        var items = new List<AudioQueueItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new AudioQueueItem
            {
                MediaId = Guid.NewGuid(),
                IndexedFileId = Guid.NewGuid(),
                Title = $"T{i}",
                Artist = "A",
                AlbumTitle = "Al"
            });
        }

        return items;
    }
}
