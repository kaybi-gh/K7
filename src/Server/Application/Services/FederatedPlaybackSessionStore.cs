using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

public sealed class FederatedPlaybackSessionStore : IFederatedPlaybackSessionStore
{
    private readonly ConcurrentDictionary<Guid, FederatedPlaybackSessionState> _sessions = new();

    public void Set(FederatedPlaybackSessionState state) =>
        _sessions[state.LocalSessionId] = state;

    public FederatedPlaybackSessionState? Get(Guid localSessionId) =>
        _sessions.TryGetValue(localSessionId, out var state) ? state : null;

    public void Remove(Guid localSessionId) =>
        _sessions.TryRemove(localSessionId, out _);
}
