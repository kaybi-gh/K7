using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DownloadQueueKeepAliveTests
{
    [Test]
    public void RequiresKeepAlive_ShouldBeFalse_WhenQueueIsEmpty()
    {
        DownloadQueueKeepAlive.RequiresKeepAlive([]).Should().BeFalse();
    }

    [Test]
    public void RequiresKeepAlive_ShouldBeFalse_WhenOnlyCacheItemsAreActive()
    {
        var queue = new[]
        {
            CreateItem(DownloadItemStatus.Downloading, isCacheItem: true)
        };

        DownloadQueueKeepAlive.RequiresKeepAlive(queue).Should().BeFalse();
    }

    [Test]
    public void RequiresKeepAlive_ShouldBeFalse_WhenUserItemsAreFinished()
    {
        var queue = new[]
        {
            CreateItem(DownloadItemStatus.Completed),
            CreateItem(DownloadItemStatus.Failed),
            CreateItem(DownloadItemStatus.Cancelled)
        };

        DownloadQueueKeepAlive.RequiresKeepAlive(queue).Should().BeFalse();
    }

    [Test]
    [TestCase(DownloadItemStatus.Queued)]
    [TestCase(DownloadItemStatus.Preparing)]
    [TestCase(DownloadItemStatus.Downloading)]
    public void RequiresKeepAlive_ShouldBeTrue_WhenUserItemIsActive(DownloadItemStatus status)
    {
        var queue = new[]
        {
            CreateItem(status)
        };

        DownloadQueueKeepAlive.RequiresKeepAlive(queue).Should().BeTrue();
    }

    [Test]
    public void CreateSnapshot_ShouldBeEmpty_WhenQueueHasNoUserDownloads()
    {
        var snapshot = DownloadQueueKeepAlive.CreateSnapshot(
        [
            CreateItem(DownloadItemStatus.Completed),
            CreateItem(DownloadItemStatus.Downloading, isCacheItem: true)
        ]);

        snapshot.ActiveCount.Should().Be(0);
        snapshot.Current.Should().BeNull();
    }

    [Test]
    public void CreateSnapshot_ShouldPreferDownloadingItem_WhenMultipleAreActive()
    {
        var queued = CreateItem(DownloadItemStatus.Queued, title: "Queued");
        var preparing = CreateItem(DownloadItemStatus.Preparing, title: "Preparing");
        var downloading = CreateItem(DownloadItemStatus.Downloading, title: "Downloading");

        var snapshot = DownloadQueueKeepAlive.CreateSnapshot([queued, preparing, downloading]);

        snapshot.ActiveCount.Should().Be(3);
        snapshot.Current.Should().BeSameAs(downloading);
    }

    [Test]
    public void CreateSnapshot_ShouldIgnoreCacheItems()
    {
        var cache = CreateItem(DownloadItemStatus.Downloading, isCacheItem: true, title: "Cache");
        var user = CreateItem(DownloadItemStatus.Queued, title: "User");

        var snapshot = DownloadQueueKeepAlive.CreateSnapshot([cache, user]);

        snapshot.ActiveCount.Should().Be(1);
        snapshot.Current.Should().BeSameAs(user);
    }

    private static DownloadQueueItem CreateItem(
        DownloadItemStatus status,
        bool isCacheItem = false,
        string title = "Title")
    {
        return new DownloadQueueItem
        {
            DownloadId = Guid.NewGuid(),
            Request = new DownloadRequest
            {
                IndexedFileId = Guid.NewGuid(),
                MediaId = Guid.NewGuid(),
                Title = title,
                MediaType = MediaType.MusicTrack,
                IsCacheItem = isCacheItem
            },
            Status = status
        };
    }
}
