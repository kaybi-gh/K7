using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
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

    [Test]
    public async Task GetOrCreateSessionAsync_ShouldSendStartSeconds_WhenResumeOffsetIsSet()
    {
        var captured = await CreateSessionAndCaptureRequestAsync(startSeconds: 5390);

        captured.Should().NotBeNull();
        captured!.StartSeconds.Should().Be(5390);
    }

    [Test]
    public async Task GetOrCreateSessionAsync_ShouldOmitStartSeconds_WhenNearStart()
    {
        var captured = await CreateSessionAndCaptureRequestAsync(startSeconds: 0.5);

        captured.Should().NotBeNull();
        captured!.StartSeconds.Should().BeNull();
    }

    private static async Task<CreateStreamSessionRequest?> CreateSessionAndCaptureRequestAsync(double startSeconds)
    {
        var indexedFileId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var streaming = Substitute.For<IStreamingService>();
        var server = Substitute.For<IK7ServerService>();
        var storage = Substitute.For<IDeviceStorageService>();
        storage.Get(PreferenceKeys.DEVICE_ID).Returns(deviceId.ToString());
        storage.Get(PreferenceKeys.STREAMING_QUALITY_WIFI, 0).Returns(0);
        storage.Get(PreferenceKeys.VIDEO_AUDIO_PASSTHROUGH, true).Returns(true);

        CreateStreamSessionRequest? captured = null;
        streaming.CreateStreamSessionAsync(
                Arg.Do<CreateStreamSessionRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(new StreamingSessionDto
            {
                Id = Guid.NewGuid(),
                IndexedFileId = indexedFileId,
                PlaybackSettings = new PlaybackSettingsDto()
            });

        var sut = new StreamUriService(streaming, server, storage);
        await sut.GetOrCreateSessionAsync(indexedFileId, startSeconds: startSeconds);
        return captured;
    }
}
