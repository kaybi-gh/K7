using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Shared.Constants;
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

    [Test]
    public async Task StartBackgroundExtract_ShouldNotExposeTheFileBeforeExtractionEnds()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "k7-vtt-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, "0.vtt");
        var halfWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcoder = Substitute.For<IMediaTranscoder>();
        transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var path = call.ArgAt<string>(2);
                await File.WriteAllTextAsync(path, "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nfirst half\n");
                halfWritten.SetResult();
                await finish.Task;
                await File.AppendAllTextAsync(path, "\n00:00:03.000 --> 00:00:04.000\nsecond half\n");
            });

        try
        {
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                transcoder, "/media/movie.mkv", 0, outputPath, Substitute.For<ILogger>());
            // Polls arriving while the extraction runs share it.
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                transcoder, "/media/movie.mkv", 0, outputPath, Substitute.For<ILogger>());

            await halfWritten.Task.WaitAsync(TimeSpan.FromSeconds(2));
            HlsSubtitleVttExtractor.IsReady(outputPath).Should().BeFalse();

            finish.SetResult();
            await WaitUntilReadyAsync(outputPath, TimeSpan.FromSeconds(2));
            (await File.ReadAllTextAsync(outputPath)).Should().Contain("second half");
            Directory.GetFiles(outputDir).Should().ContainSingle();
            await transcoder.ReceivedWithAnyArgs(1)
                .ExtractSubtitleAsVttAsync(default!, default, default!, default);
        }
        finally
        {
            finish.TrySetResult();
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestCase(0, 0)]
    [TestCase(119.9, 0)]
    [TestCase(120, 1)]
    [TestCase(3700, 30)]
    [TestCase(-5, 0)]
    [TestCase(double.NaN, 0)]
    public void GetWindowIndex_ShouldMapPositionToItsWindow(double positionSeconds, int expected)
    {
        HlsSubtitleVttExtractor.GetWindowIndex(positionSeconds).Should().Be(expected);
    }

    [Test]
    public void WindowSeconds_ShouldBeAMultipleOfTheHlsSubtitleSegmentDuration()
    {
        // A segment served from a window must lie inside that window.
        (HlsSubtitleVttExtractor.WindowSeconds % 30).Should().Be(0);
    }

    [Test]
    public void FindReadyPath_ShouldPreferThePreExtractedTrack()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-vtt-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsConfiguration
        {
            Transcoding = Path.Combine(root, "transcoding"),
            Metadatas = Path.Combine(root, "metadatas")
        };
        var id = Guid.NewGuid();
        var cachePath = HlsSubtitleVttExtractor.GetCachePath(paths.Transcoding, id, 2);
        var persistentPath = HlsSubtitleVttExtractor.GetPersistentCachePath(paths.Metadatas, id, 2);
        persistentPath.Should().Be(Path.Combine(
            paths.Metadatas, "indexed-files", id.ToString(), "subtitles", "2.vtt"));

        try
        {
            HlsSubtitleVttExtractor.FindReadyPath(paths, id, 2).Should().BeNull();

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, "WEBVTT\n\n");
            HlsSubtitleVttExtractor.FindReadyPath(paths, id, 2).Should().Be(cachePath);

            Directory.CreateDirectory(Path.GetDirectoryName(persistentPath)!);
            File.WriteAllText(persistentPath, "WEBVTT\n\n");
            HlsSubtitleVttExtractor.FindReadyPath(paths, id, 2).Should().Be(persistentPath);

            HlsSubtitleVttExtractor.DeletePersistentCache(paths, id, Substitute.For<ILogger>());
            HlsSubtitleVttExtractor.FindReadyPath(paths, id, 2).Should().Be(cachePath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReadWindowAsync_ShouldReturnNullThenContent_AndPrepareTheNextWindow()
    {
        var transcodingPath = Path.Combine(Path.GetTempPath(), "k7-vtt-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var transcoder = Substitute.For<IMediaTranscoder>();
        transcoder.ExtractSubtitleWindowAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => File.WriteAllTextAsync(
                call.ArgAt<string>(4),
                $"WEBVTT\n\nwindow from {call.ArgAt<TimeSpan>(2).TotalSeconds} to {call.ArgAt<TimeSpan>(3).TotalSeconds}\n"));

        try
        {
            Task<string?> ReadAsync(int windowIndex) => HlsSubtitleVttExtractor.ReadWindowAsync(
                transcoder, "/media/movie.mkv", transcodingPath, id, 2, windowIndex, Substitute.For<ILogger>(), CancellationToken.None);

            (await ReadAsync(2)).Should().BeNull();

            var window2 = HlsSubtitleVttExtractor.GetWindowCachePath(transcodingPath, id, 2, 2);
            var window3 = HlsSubtitleVttExtractor.GetWindowCachePath(transcodingPath, id, 2, 3);
            await WaitUntilAsync(() => File.Exists(window2) && File.Exists(window3));

            // Look-behind of 30 s before the window, never before the start of the file.
            (await ReadAsync(2)).Should().Contain("window from 210 to 360");
            (await File.ReadAllTextAsync(window3)).Should().Contain("window from 330 to 480");

            (await ReadAsync(0)).Should().BeNull();
            var window0 = HlsSubtitleVttExtractor.GetWindowCachePath(transcodingPath, id, 2, 0);
            await WaitUntilAsync(() => File.Exists(window0));
            (await ReadAsync(0)).Should().Contain("window from 0 to 120");
        }
        finally
        {
            if (Directory.Exists(transcodingPath))
                Directory.Delete(transcodingPath, recursive: true);
        }
    }

    [Test]
    public async Task ReadPartialAsync_ShouldJoinTheContiguousWindowsAroundThePosition()
    {
        var transcodingPath = Path.Combine(Path.GetTempPath(), "k7-vtt-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var transcoder = Substitute.For<IMediaTranscoder>();

        async Task WriteWindowAsync(int windowIndex, string cue)
        {
            var path = HlsSubtitleVttExtractor.GetWindowCachePath(transcodingPath, id, 2, windowIndex);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "WEBVTT\n\n" + cue + "\n");
        }

        try
        {
            // Windows 1 and 2 touch the position, window 5 is the leftover of an earlier seek.
            await WriteWindowAsync(1, "02:30.000 --> 02:32.000\nin window 1");
            await WriteWindowAsync(2, "04:30.000 --> 04:32.000\nin window 2");
            await WriteWindowAsync(5, "10:30.000 --> 10:32.000\nin window 5");

            var partial = await HlsSubtitleVttExtractor.ReadPartialAsync(
                transcoder, "/media/movie.mkv", transcodingPath, id, 2, positionSeconds: 150, Substitute.For<ILogger>(), CancellationToken.None);

            partial.Should().NotBeNull();
            SubtitleVttFormat.IsPartial(partial).Should().BeTrue();
            partial.Should().Contain("in window 1").And.Contain("in window 2");
            partial.Should().NotContain("in window 5");

            var missing = await HlsSubtitleVttExtractor.ReadPartialAsync(
                transcoder, "/media/movie.mkv", transcodingPath, id, 2, positionSeconds: 500, Substitute.For<ILogger>(), CancellationToken.None);

            missing.Should().BeNull();
            await transcoder.Received(1).ExtractSubtitleWindowAsVttAsync(
                "/media/movie.mkv",
                2,
                TimeSpan.FromSeconds(450),
                TimeSpan.FromSeconds(600),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(transcodingPath))
                Directory.Delete(transcodingPath, recursive: true);
        }
    }

    private static Task WaitUntilReadyAsync(string path, TimeSpan timeout) =>
        WaitUntilAsync(() => HlsSubtitleVttExtractor.IsReady(path), timeout);

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }
    }
}
