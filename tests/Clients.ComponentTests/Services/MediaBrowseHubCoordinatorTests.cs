using System.Reflection;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MediaBrowseHubCoordinatorTests
{
    [Test]
    public void MediaPicturesUpdated_ShouldPreferVisualCallback_WhenProvided()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        Guid? visualMediaId = null;
        using var _ = sut.Subscribe(
            libraryIds: null,
            libraryGroupIds: [Guid.NewGuid()],
            onCatalogChanged: () => catalogCalls++,
            onMediaVisualChanged: id => visualMediaId = id);

        var mediaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Raise(hub, nameof(K7HubClient.MediaPicturesUpdated), mediaId);

        catalogCalls.Should().Be(0);
        visualMediaId.Should().Be(mediaId);
    }

    [Test]
    public void MediaPicturesUpdated_ShouldFallBackToCatalog_WhenVisualCallbackMissing()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        using var _ = sut.Subscribe(
            libraryIds: null,
            libraryGroupIds: [Guid.NewGuid()],
            onCatalogChanged: () => catalogCalls++);

        Raise(hub, nameof(K7HubClient.MediaPicturesUpdated), Guid.NewGuid());

        catalogCalls.Should().Be(1);
    }

    [Test]
    public void MediaBatchAdded_ShouldNotifyCatalog_NotVisual()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        var visualCalls = 0;
        var libraryId = Guid.NewGuid();
        using var _ = sut.Subscribe(
            libraryIds: [libraryId],
            libraryGroupIds: [Guid.NewGuid()],
            onCatalogChanged: () => catalogCalls++,
            onMediaVisualChanged: _ => visualCalls++,
            mediaTypes: [MediaType.Movie]);

        Raise(hub, nameof(K7HubClient.MediaBatchAdded), new List<MediaBatchItem>
        {
            new()
            {
                MediaId = Guid.NewGuid(),
                MediaType = nameof(MediaType.Movie),
                LibraryId = libraryId
            }
        });

        catalogCalls.Should().Be(1);
        visualCalls.Should().Be(0);
    }

    [Test]
    public void MediaBatchAdded_ShouldIgnore_WhenMediaTypeDoesNotMatch()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        var libraryId = Guid.NewGuid();
        using var _ = sut.Subscribe(
            libraryIds: [libraryId],
            libraryGroupIds: null,
            onCatalogChanged: () => catalogCalls++,
            mediaTypes: [MediaType.Movie]);

        Raise(hub, nameof(K7HubClient.MediaBatchAdded), new List<MediaBatchItem>
        {
            new()
            {
                MediaId = Guid.NewGuid(),
                MediaType = nameof(MediaType.MusicAlbum),
                LibraryId = libraryId
            }
        });

        catalogCalls.Should().Be(0);
    }

    [Test]
    public void MediaBatchAdded_ShouldIgnore_WhenLibraryDoesNotMatch()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        using var _ = sut.Subscribe(
            libraryIds: [Guid.NewGuid()],
            libraryGroupIds: [Guid.NewGuid()],
            onCatalogChanged: () => catalogCalls++,
            mediaTypes: [MediaType.Movie]);

        Raise(hub, nameof(K7HubClient.MediaBatchAdded), new List<MediaBatchItem>
        {
            new()
            {
                MediaId = Guid.NewGuid(),
                MediaType = nameof(MediaType.Movie),
                LibraryId = Guid.NewGuid()
            }
        });

        catalogCalls.Should().Be(0);
    }

    [Test]
    public void LibraryScanCompleted_ShouldIgnore_WhenLibraryGroupOnlyWithoutLibraryIds()
    {
        var hub = new K7HubClient(NullLogger<K7HubClient>.Instance);
        using var sut = new MediaBrowseHubCoordinator(hub);

        var catalogCalls = 0;
        using var _ = sut.Subscribe(
            libraryIds: null,
            libraryGroupIds: [Guid.NewGuid()],
            onCatalogChanged: () => catalogCalls++);

        Raise(hub, nameof(K7HubClient.LibraryScanCompleted), Guid.NewGuid(), 1, 0, 0);

        catalogCalls.Should().Be(0);
    }

    private static void Raise<T>(object target, string eventName, T arg)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Event field '{eventName}' not found.");
        var handler = field.GetValue(target) as MulticastDelegate;
        handler?.DynamicInvoke(arg);
    }

    private static void Raise(object target, string eventName, Guid libraryId, int added, int skipped, int inaccessible)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Event field '{eventName}' not found.");
        var handler = field.GetValue(target) as MulticastDelegate;
        handler?.DynamicInvoke(libraryId, added, skipped, inaccessible);
    }
}

[TestFixture]
public class MediaBrowseCarouselRefreshScopeTests
{
    [Test]
    public void IsAffected_ShouldMatchLibraryIds()
    {
        var libraryId = Guid.NewGuid();
        MediaBrowseCarouselRefreshScope.IsAffected([libraryId], null, libraryId).Should().BeTrue();
        MediaBrowseCarouselRefreshScope.IsAffected([libraryId], null, Guid.NewGuid()).Should().BeFalse();
    }

    [Test]
    public void IsAffected_ShouldIgnoreLibraryEvents_WhenOnlyLibraryGroupIds()
    {
        MediaBrowseCarouselRefreshScope.IsAffected(null, [Guid.NewGuid()], Guid.NewGuid()).Should().BeFalse();
    }

    [Test]
    public void IsBatchAffected_ShouldRequireMatchingTypeAndLibrary()
    {
        var libraryId = Guid.NewGuid();
        var items = new List<MediaBatchItem>
        {
            new()
            {
                MediaId = Guid.NewGuid(),
                MediaType = nameof(MediaType.Movie),
                LibraryId = libraryId
            }
        };

        MediaBrowseCarouselRefreshScope.IsBatchAffected(
            [libraryId], null, [MediaType.Movie], items).Should().BeTrue();

        MediaBrowseCarouselRefreshScope.IsBatchAffected(
            [libraryId], null, [MediaType.MusicAlbum], items).Should().BeFalse();

        MediaBrowseCarouselRefreshScope.IsBatchAffected(
            [Guid.NewGuid()], null, [MediaType.Movie], items).Should().BeFalse();
    }
}

[TestFixture]
public class LruCacheTests
{
    [Test]
    public void Snapshot_ShouldReturnEntriesInMruOrder()
    {
        var cache = new LruCache<int, string>(3);
        cache.Set(1, "a");
        cache.Set(2, "b");
        cache.Set(3, "c");
        _ = cache.TryGetValue(1, out _);

        cache.Snapshot().Select(e => e.Key).Should().Equal(1, 3, 2);
    }
}
