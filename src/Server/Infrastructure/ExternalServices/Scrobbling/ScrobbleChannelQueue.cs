using System.Threading.Channels;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

public sealed class ScrobbleChannelQueue : IScrobbleQueue
{
    private readonly Channel<ScrobbleWorkItem> _channel = Channel.CreateUnbounded<ScrobbleWorkItem>();

    public void Enqueue(ScrobbleWorkItem item) => _channel.Writer.TryWrite(item);

    public IAsyncEnumerable<ScrobbleWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
