using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSubtitleVttExtractorTests
{
    [Test]
    public void GetCachePath_ShouldUseSubtitlesFolderAndTrackIndex()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var path = HlsSubtitleVttExtractor.GetCachePath("/cache", id, 3);

        path.Should().Be(Path.Combine(
            "/cache",
            id.ToString("N"),
            Hls.SubtitlesCacheDirectoryName,
            "3.vtt"));
    }

    [Test]
    public void IsReady_ShouldBeFalse_WhenFileMissing()
    {
        HlsSubtitleVttExtractor.IsReady(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".vtt"))
            .Should().BeFalse();
    }

    [Test]
    public async Task StartBackgroundExtract_ShouldReturnImmediately_AndWriteCacheLater()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "k7-vtt-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, "0.vtt");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcoder = Substitute.For<IMediaTranscoder>();
        transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(200);
                var path = call.ArgAt<string>(2);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "WEBVTT\n\n");
            });

        try
        {
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                transcoder,
                "/media/movie.mkv",
                0,
                outputPath,
                Substitute.For<ILogger>());

            HlsSubtitleVttExtractor.IsReady(outputPath).Should().BeFalse();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await WaitUntilReadyAsync(outputPath, TimeSpan.FromSeconds(2));
            HlsSubtitleVttExtractor.IsReady(outputPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    private static async Task WaitUntilReadyAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (HlsSubtitleVttExtractor.IsReady(path))
                return;
            await Task.Delay(20);
        }
    }
}
