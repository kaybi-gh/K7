using System.Collections.Concurrent;

namespace K7.Server.Web.Services;

public interface IAdminStreamAudience
{
    void Track(string connectionId, bool isTv);

    void Forget(string connectionId);

    IReadOnlyList<string> TvConnectionIds { get; }
}

internal sealed class AdminStreamAudience : IAdminStreamAudience
{
    private readonly ConcurrentDictionary<string, byte> _tvConnectionIds = new();

    public void Track(string connectionId, bool isTv)
    {
        if (isTv)
            _tvConnectionIds[connectionId] = 0;
        else
            _tvConnectionIds.TryRemove(connectionId, out _);
    }

    public void Forget(string connectionId) =>
        _tvConnectionIds.TryRemove(connectionId, out _);

    public IReadOnlyList<string> TvConnectionIds => _tvConnectionIds.Keys.ToList();
}
