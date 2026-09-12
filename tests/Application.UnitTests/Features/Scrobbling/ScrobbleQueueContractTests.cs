using System.Threading.Channels;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Features.Scrobbling;

[TestFixture]
public class ScrobbleQueueContractTests
{
    [Test]
    public async Task UnboundedChannelQueue_ShouldDeliverEnqueuedItems()
    {
        var queue = new TestQueue();
        var item = new ScrobbleWorkItem
        {
            AccountId = Guid.NewGuid(),
            Provider = ScrobblerProvider.ListenBrainz,
            ConfigJson = "{}",
            Payload = new ScrobblePayload
            {
                UserId = Guid.NewGuid(),
                MediaId = Guid.NewGuid(),
                MediaType = MediaType.Movie,
                Title = "T",
                State = PlaybackState.Ended,
                PositionSeconds = 1,
                DurationSeconds = 1,
                IsCompleted = true
            }
        };

        queue.Enqueue(item);
        queue.Complete();

        var read = new List<ScrobbleWorkItem>();
        await foreach (var work in queue.ReadAllAsync(CancellationToken.None))
            read.Add(work);

        read.Should().ContainSingle().Which.AccountId.Should().Be(item.AccountId);
    }

    private sealed class TestQueue : IScrobbleQueue
    {
        private readonly Channel<ScrobbleWorkItem> _channel = Channel.CreateUnbounded<ScrobbleWorkItem>();

        public void Enqueue(ScrobbleWorkItem item) => _channel.Writer.TryWrite(item);

        public void Complete() => _channel.Writer.TryComplete();

        public IAsyncEnumerable<ScrobbleWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
