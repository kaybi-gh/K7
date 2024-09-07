using System.Drawing;
using FFMpegCore;
using MediaServer.Application.Extensions;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.ValueObjects;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.UnitTests.Extensions;

public class IndexedFileExtensionsTests : FileFixture
{
    [TestCase("", "1884 (1995).mp4", "1884", 1995)]
    [TestCase("", "1884 - 1995.mp4", "1884", 1995)]
    [TestCase("", "Movie (1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie 1884 (1995).mp4", "Movie 1884", 1995)]
    [TestCase("", "Movie 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie(1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie.1990.noise.mp4", "Movie", 1990)]
    [TestCase("", "Movie.noise.1990.mp4", "Movie.noise", 1990)]
    [TestCase("", "Movie.noise.1990.noise.mp4", "Movie.noise", 1990)]
    [TestCase("", "The incredible movie (1995).mp4", "The incredible movie", 1995)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("Movie (2000)", "Movie (2001).mkv", "Movie", 2001)]
    [TestCase("Movie (2000)", "Movie.mkv", "Movie", 2000)]
    [TestCase("Movie (2000)", "RandomFileTitle.mkv", "Movie", 2000)]
    [TestCase("Movie (2005)", "Movie cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.part1.avi", "Movie", 2005)]
    [TestCase(null, "Movie(1990).mp4", "Movie", 1990)]
    public async Task ShouldParseMovieFileCorrectly(string? parentDirectory, string fileName, string expectedMovieTitle, int expectedReleaseYear)
    {
        // Arrange
        var path = string.IsNullOrEmpty(parentDirectory) ? fileName : Path.Combine(parentDirectory, fileName);
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library()
        {
            Id = Guid.NewGuid(),
            MediaType = LibraryMediaType.Movie,
            RootPath = FileHelper.TestDirectoryPath,
            Title = "Movies"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id);

        // Act
        indexedFile!.TryIdentifyMovie(out MediaIdentification? identification);

        try
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "C:\\ProgramData\\chocolatey\\lib\\ffmpeg\\tools\\ffmpeg\\bin" });
            var inputPath = "C:\\test\\Aquaman (2023)\\Aquaman and the Lost Kingdom (2023).mkv";
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            var computedWidth = (double)144 * mediaInfo.PrimaryVideoStream!.Width / mediaInfo.PrimaryVideoStream!.Height;
            for (int i = 1; i < mediaInfo.Duration.TotalSeconds / 10; i++)
            {
                FFMpeg.Snapshot(inputPath, $"C:\\test\\Aquaman (2023)\\thumbnails\\thumbnail-{i}", new Size((int)computedWidth, 144), TimeSpan.FromSeconds(i*10));
            }   
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        // Assert
        //indexedFile!.IsIdentified.Should().BeTrue();
        identification!.Title.Should().Be(expectedMovieTitle);
        identification!.ReleaseYear!.Value.Year.Should().Be(expectedReleaseYear);
    }

    [Test]
    public void TestKeyframes()
    {
        var id = Guid.NewGuid();
        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "C:\\ProgramData\\chocolatey\\lib\\ffmpeg\\tools\\ffmpeg\\bin" });
        var inputPath = "C:\\test\\Aquaman (2023)\\Aquaman and the Lost Kingdom (2023).mkv";

        var ffprobeResult = FFProbe.GetPackets(inputPath);
        var keyframes = ffprobeResult.Packets
            .Where(x => x.StreamIndex == 0)
            .Where(x => x.CodecType == "video")
            .Where(x => x.Flags.Contains("K"))
            .ToList();

        var segments = new List<HlsSegment>();
        var currentKeyframes = new List<int>();
        double currentSegmentDuration = 0.0;
        var segmentId = 0;

        for (int i = 1; i < keyframes.Count; i++)
        {
            currentSegmentDuration += keyframes[i].Pts - keyframes[i - 1].Pts;
            currentKeyframes.Add((int)keyframes[i - 1].Pts);

            if (currentSegmentDuration >= 10000)
            {
                foreach (var keyframe in currentKeyframes)
                {
                    var duration = TimeSpan.FromMilliseconds(currentSegmentDuration);
                    segments.Add(new HlsSegment()
                    {
                        VideoFileMetadataId = id,
                        SegmentId = segmentId,
                        Duration = duration,
                        Keyframe = new TimeOnly(0, 0 ,0, keyframe)
                    });
                }
                segmentId++;
                currentKeyframes.Clear();
                currentSegmentDuration = 0.0;
            }
        }
    }
}
