using System.Threading.Channels;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false
    });

    public void Enqueue(Guid taskId)
    {
        _channel.Writer.TryWrite(taskId);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
