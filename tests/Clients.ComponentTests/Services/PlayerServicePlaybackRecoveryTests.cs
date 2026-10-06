using K7.Clients.Shared.Interfaces;
using K7.Clients.Web.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class PlayerServicePlaybackRecoveryTests
{
    private IStreamUriService _streamUri = null!;
    private IDeviceStorageService _storage = null!;
    private PlayerService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _streamUri = Substitute.For<IStreamUriService>();
        _storage = Substitute.For<IDeviceStorageService>();
        _storage.Get(Arg.Any<PreferenceKey<int>>(), Arg.Any<int>())
            .Returns(ci => ci.ArgAt<int>(1));
        _storage.Get(Arg.Any<PreferenceKey<double>>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(1));
        _storage.Get(Arg.Any<PreferenceKey<bool>>(), Arg.Any<bool>())
            .Returns(ci => ci.ArgAt<bool>(1));
        _storage.Get(Arg.Any<PreferenceKey<string?>>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string?>(1));

        _sut = new PlayerService(
            _streamUri,
            _storage,
            Substitute.For<IUserPreferencesService>());
    }

    [Test]
    public async Task TryRecoverPlaybackStartAsync_ShouldStayOnOriginal_WhenRemuxInitIsNotReadyYet()
    {
        await StartPlaybackAsync();

        var recovered = await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: true);

        recovered.Should().BeTrue();
        _sut.SelectedQuality!.IsOriginal.Should().BeTrue();
        _sut.Source.Url.Should().NotContain("Quality=");
    }

    [Test]
    public async Task TryRecoverPlaybackStartAsync_ShouldKeepOriginal_ThroughRepeatedColdStartErrors()
    {
        await StartPlaybackAsync();

        (await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: true)).Should().BeTrue();
        (await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: true)).Should().BeTrue();
        (await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: true)).Should().BeTrue();
        _sut.SelectedQuality!.IsOriginal.Should().BeTrue();
    }

    [Test]
    public async Task TryRecoverPlaybackStartAsync_ShouldReturnFalse_WhenQualityLadderDisabled()
    {
        await StartPlaybackAsync();

        var recovered = await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: false);

        recovered.Should().BeFalse();
        _sut.SelectedQuality!.IsOriginal.Should().BeTrue();
    }

    [Test]
    public async Task TryRecoverPlaybackStartAsync_ShouldReturnTrue_WhenAlreadyPlaying()
    {
        await StartPlaybackAsync();
        _sut.PlaybackState = PlaybackState.Playing;
        _sut.CurrentTime = 12;

        var recovered = await _sut.TryRecoverPlaybackStartAsync(allowQualityLadder: true);

        recovered.Should().BeTrue();
        _sut.SelectedQuality!.IsOriginal.Should().BeTrue();
    }

    [Test]
    public async Task TryRecoverHlsAudioRemuxAsync_ShouldReloadWithAacTranscode_WhenAudioAppendFails()
    {
        await StartPlaybackAsync(audioTrackIndex: 1);

        var recovered = await _sut.TryRecoverHlsAudioRemuxAsync(
            "audio append of 288925b failed for segment #8 in playlist 0");

        recovered.Should().BeTrue();
        _sut.Source.Url.Should().Contain("AudioTrackTranscodings=1%3Aaac");
        _sut.SelectedQuality!.IsOriginal.Should().BeTrue();
    }

    [Test]
    public async Task TryRecoverHlsAudioRemuxAsync_ShouldRetryOnce()
    {
        await StartPlaybackAsync(audioTrackIndex: 0);

        (await _sut.TryRecoverHlsAudioRemuxAsync("audio append failed")).Should().BeTrue();
        var url = _sut.Source.Url;
        (await _sut.TryRecoverHlsAudioRemuxAsync("audio append failed")).Should().BeFalse();
        _sut.Source.Url.Should().Be(url);
    }

    [Test]
    public async Task TryRecoverHlsAudioRemuxAsync_ShouldIgnoreUnrelatedErrors()
    {
        await StartPlaybackAsync(audioTrackIndex: 0);

        var recovered = await _sut.TryRecoverHlsAudioRemuxAsync("network error");

        recovered.Should().BeFalse();
        _sut.Source.Url.Should().NotContain("AudioTrackTranscodings");
    }

    private async Task StartPlaybackAsync(int? audioTrackIndex = null)
    {
        var sessionId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _streamUri.GetOrCreateSessionAsync(
                fileId,
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<double?>(),
                Arg.Any<CancellationToken>())
            .Returns(new StreamingSessionDto
            {
                Id = sessionId,
                IndexedFileId = fileId,
                PlaybackSettings = new PlaybackSettingsDto(),
                Source = new IndexedFileStreamUri
                {
                    Uri = new Uri($"/api/indexed-files/{fileId}/hls-stream/manifest.m3u8?streamSessionId={sessionId}", UriKind.Relative),
                    MimeType = "application/vnd.apple.mpegurl"
                }
            });

        var audioTracks = audioTrackIndex is int index
            ? new[] { new AudioFileTrackDto { Index = index, Codec = "aac", Channels = 2, IsDefault = true } }
            : Array.Empty<AudioFileTrackDto>();

        await _sut.PlayIndexedFileAsync(
            fileId,
            audioTracks,
            audioTrackIndex: audioTrackIndex,
            videoResolution: VideoResolutionIdentifier._1080p);
    }
}
