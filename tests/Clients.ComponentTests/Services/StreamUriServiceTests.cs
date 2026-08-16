using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class StreamUriServiceTests
{
    [Test]
    public async Task GetOrCreateSessionAsync_ShouldReturnFileUri_WhenOfflineFileExists()
    {
        var temp = Path.GetTempFileName();
        try
        {
            var indexedFileId = Guid.NewGuid();
            var streaming = Substitute.For<IStreamingService>();
            var server = Substitute.For<IK7ServerService>();
            var storage = Substitute.For<IDeviceStorageService>();
            var offline = Substitute.For<IOfflineMediaStore>();
            offline.GetByIndexedFileIdAsync(indexedFileId, Arg.Any<CancellationToken>())
                .Returns(new DownloadedMediaItem
                {
                    Id = Guid.NewGuid(),
                    IndexedFileId = indexedFileId,
                    MediaId = Guid.NewGuid(),
                    MediaType = MediaType.Movie,
                    Title = "Offline movie",
                    MediaLocalPath = temp,
                    FileSize = 1,
                    DownloadedAt = DateTimeOffset.UtcNow,
                    IsCacheItem = false
                });

            var sut = new StreamUriService(streaming, server, storage, offline);

            var session = await sut.GetOrCreateSessionAsync(indexedFileId);

            session.Source.Should().NotBeNull();
            session.Source!.Uri.IsAbsoluteUri.Should().BeTrue();
            session.Source.Uri.Scheme.Should().Be(Uri.UriSchemeFile);
            Path.GetFullPath(session.Source.Uri.LocalPath).Should().Be(Path.GetFullPath(temp));
            await streaming.DidNotReceiveWithAnyArgs().CreateStreamSessionAsync(default!, default);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
