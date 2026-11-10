using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MusicRadioPlaybackServiceTests
{
    private static readonly MusicRadioRequest _request = new() { RadioType = "Sonic", Title = "Radio" };

    private IServerInfoService _serverInfo = null!;
    private IAudioPlayerService _audio = null!;
    private List<AudioQueueItem> _queue = null!;
    private List<AudioQueueItem> _history = null!;
    private Guid[]? _lastExcludeIds;
    private MusicRadioPlaybackService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _serverInfo = Substitute.For<IServerInfoService>();
        _audio = Substitute.For<IAudioPlayerService>();
        _queue = [];
        _history = [];
        _lastExcludeIds = null;

        _audio.Queue.Returns(_ => _queue);
        _audio.PlayHistory.Returns(_ => _history);
        _audio.CurrentIndex.Returns(_ => _queue.Count == 0 ? -1 : 0);
        _audio.ActiveRadioTitle.Returns(_request.Title);
        _audio.When(a => a.AddToQueue(Arg.Any<IReadOnlyList<AudioQueueItem>>()))
            .Do(call => _queue.AddRange(call.Arg<IReadOnlyList<AudioQueueItem>>()));

        _sut = new MusicRadioPlaybackService(
            _serverInfo,
            _audio,
            Substitute.For<IK7ServerService>(),
            Substitute.For<IUserRatingSync>(),
            NullLogger<MusicRadioPlaybackService>.Instance);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public async Task ExtendAsync_ShouldQueueMoreTracksOfTheActiveRadio_WhenTheQueueIsAtItsEnd()
    {
        _queue.Add(QueueItem());
        _sut.AttachWithoutPlaying(_request);
        GivenTheServerReturns(10);

        var extended = await _sut.ExtendAsync();

        extended.Should().BeTrue();
        _queue.Should().HaveCount(11);
        await _serverInfo.Received(1).GetMusicRadioAsync(
            _request.RadioType,
            Arg.Any<Guid[]?>(), Arg.Any<Guid[]?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<int>(), Arg.Any<Guid[]?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExtendAsync_ShouldLeaveOutTheTracksAlreadyPlayed()
    {
        var current = QueueItem();
        var played = QueueItem();
        _queue.Add(current);
        _history.Add(played);
        _sut.AttachWithoutPlaying(_request);
        GivenTheServerReturns(10);

        await _sut.ExtendAsync();

        _lastExcludeIds.Should().BeEquivalentTo([current.MediaId, played.MediaId]);
    }

    [Test]
    public async Task ExtendAsync_ShouldReturnFalse_WhenTheRadioHasNothingLeft()
    {
        _queue.Add(QueueItem());
        _sut.AttachWithoutPlaying(_request);
        GivenTheServerReturns(0);

        var extended = await _sut.ExtendAsync();

        extended.Should().BeFalse();
        _queue.Should().HaveCount(1);
    }

    [Test]
    public async Task ExtendAsync_ShouldReturnFalseWithoutAskingTheServer_WhenNoRadioIsActive()
    {
        GivenTheServerReturns(10);

        // No request at all.
        (await _sut.ExtendAsync()).Should().BeFalse();

        // A request left over while the player no longer plays a radio.
        _sut.AttachWithoutPlaying(_request);
        _audio.ActiveRadioTitle.Returns((string?)null);
        (await _sut.ExtendAsync()).Should().BeFalse();

        _lastExcludeIds.Should().BeNull();
        _audio.DidNotReceive().AddToQueue(Arg.Any<IReadOnlyList<AudioQueueItem>>());
    }

    [Test]
    public async Task ExtendAsync_ShouldNotAskTheServer_WhenTracksAreStillQueued()
    {
        _queue.AddRange(Enumerable.Range(0, 10).Select(_ => QueueItem()));
        _sut.AttachWithoutPlaying(_request);
        GivenTheServerReturns(10);

        var extended = await _sut.ExtendAsync();

        // The refill it starts afterwards has enough tracks ahead and asks for nothing either.
        extended.Should().BeTrue();
        _queue.Should().HaveCount(10);
    }

    private void GivenTheServerReturns(int count) =>
        _serverInfo.GetMusicRadioAsync(
                Arg.Any<string>(), Arg.Any<Guid[]?>(), Arg.Any<Guid[]?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<Guid[]?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _lastExcludeIds = call.ArgAt<Guid[]?>(7) ?? [];
                return Enumerable.Range(0, count)
                    .Select(i => (LiteMediaDto)new LiteMusicTrackDto { Id = Guid.NewGuid(), Title = "Track " + i, IndexedFileId = Guid.NewGuid() })
                    .ToList();
            });

    private static AudioQueueItem QueueItem() => new()
    {
        IndexedFileId = Guid.NewGuid(),
        MediaId = Guid.NewGuid(),
        Title = "Track",
        Artist = null,
        AlbumTitle = null
    };
}
