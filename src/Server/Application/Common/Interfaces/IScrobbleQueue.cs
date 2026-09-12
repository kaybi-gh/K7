using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Interfaces;

public interface IScrobbleQueue
{
    void Enqueue(ScrobbleWorkItem item);
    IAsyncEnumerable<ScrobbleWorkItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed record ScrobbleWorkItem
{
    public required Guid AccountId { get; init; }
    public required ScrobblerProvider Provider { get; init; }
    public required string ConfigJson { get; init; }
    public required ScrobblePayload Payload { get; init; }
    public int Attempts { get; init; }
}
