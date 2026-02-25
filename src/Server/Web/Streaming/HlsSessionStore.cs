using System.Collections.Concurrent;

namespace K7.Server.Web.Streaming;

internal sealed class HlsSessionInfo
{
    public required Guid SessionId { get; init; }
    public required string RootDirectory { get; init; }
}

internal interface IHlsSessionStore
{
    HlsSessionInfo Register(Guid sessionId, string rootDirectory);
    bool TryGet(Guid sessionId, out HlsSessionInfo? sessionInfo);
}

internal sealed class HlsSessionStore : IHlsSessionStore
{
    private readonly ConcurrentDictionary<Guid, HlsSessionInfo> _sessions = new();

    public HlsSessionInfo Register(Guid sessionId, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var info = new HlsSessionInfo
        {
            SessionId = sessionId,
            RootDirectory = rootDirectory
        };

        _sessions[sessionId] = info;
        return info;
    }

    public bool TryGet(Guid sessionId, out HlsSessionInfo? sessionInfo)
    {
        var found = _sessions.TryGetValue(sessionId, out var info);
        sessionInfo = info;
        return found;
    }
}
