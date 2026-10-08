using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class FederatedPlaybackSessionStoreTests
{
    [Test]
    public void SetGetRemove_ShouldRoundTrip()
    {
        var store = new FederatedPlaybackSessionStore();
        var id = Guid.NewGuid();
        store.Set(new FederatedPlaybackSessionState
        {
            LocalSessionId = id,
            RemoteSessionId = Guid.NewGuid(),
            PeerServerId = Guid.NewGuid(),
            RemoteIndexedFileId = Guid.NewGuid(),
            OriginDirectStreamUrl = "http://origin.example/direct-stream"
        });

        store.Get(id).Should().NotBeNull();
        store.Remove(id);
        store.Get(id).Should().BeNull();
    }

    [Test]
    public void ActiveStreamTrackerRemove_ShouldClearFederatedPlaybackState()
    {
        var store = new FederatedPlaybackSessionStore();
        var tracker = new ActiveStreamTracker(store);
        var id = Guid.NewGuid();
        store.Set(new FederatedPlaybackSessionState
        {
            LocalSessionId = id,
            RemoteSessionId = Guid.NewGuid(),
            PeerServerId = Guid.NewGuid(),
            RemoteIndexedFileId = Guid.NewGuid(),
            OriginDirectStreamUrl = "http://origin.example/direct-stream"
        });
        tracker.Upsert(id, new ActiveStreamInfo
        {
            SessionId = id,
            IdentityUserId = "u",
            StartedAt = DateTime.UtcNow,
            FederatedPlaybackExecution = FederatedPlaybackExecution.Peer
        });

        tracker.Remove(id);

        store.Get(id).Should().BeNull();
        tracker.GetStreamInfo(id).Should().BeNull();
    }
}
