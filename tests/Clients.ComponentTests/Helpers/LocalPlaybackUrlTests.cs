using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class LocalPlaybackUrlTests
{
    [Test]
    public void IsLocalFile_ShouldBeFalse_WhenUrlIsNullOrEmpty()
    {
        LocalPlaybackUrl.IsLocalFile(null).Should().BeFalse();
        LocalPlaybackUrl.IsLocalFile("").Should().BeFalse();
        LocalPlaybackUrl.IsLocalFile("   ").Should().BeFalse();
    }

    [Test]
    public void IsLocalFile_ShouldBeFalse_WhenUrlIsHttpStream()
    {
        LocalPlaybackUrl.IsLocalFile("https://host/api/hls-stream/abc/manifest.m3u8").Should().BeFalse();
        LocalPlaybackUrl.IsLocalFile("http://10.0.2.2:7080/api/files/1/direct-stream").Should().BeFalse();
    }

    [Test]
    public void IsLocalFile_ShouldBeTrue_WhenUrlIsFileScheme()
    {
        LocalPlaybackUrl.IsLocalFile("file:///data/user/0/com.k7/files/downloads/movies/a.mp4")
            .Should().BeTrue();
    }

    [Test]
    public void IsLocalFile_ShouldBeTrue_WhenUrlIsAndroidFilesystemPath()
    {
        LocalPlaybackUrl.IsLocalFile("/data/user/0/com.k7/files/downloads/movies/a.mp4")
            .Should().BeTrue();
    }

    [Test]
    public void IsLocalFile_ShouldBeTrue_WhenLocalPathHasStartSecondsQuery()
    {
        LocalPlaybackUrl.IsLocalFile("/data/user/0/com.k7/files/a.mp4?startSeconds=12.500")
            .Should().BeTrue();
    }

    [Test]
    public void IsLocalFile_ShouldBeFalse_WhenPathIsNotRooted()
    {
        LocalPlaybackUrl.IsLocalFile("movie.mp4").Should().BeFalse();
    }

    [Test]
    public void ToFilesystemPath_ShouldDecodeFileUri()
    {
        LocalPlaybackUrl.ToFilesystemPath("file:///data/user/0/com.k7/files/a.mp4")
            .Should().Be("/data/user/0/com.k7/files/a.mp4");
    }

    [Test]
    public void ToFilesystemPath_ShouldStripStartSecondsQuery()
    {
        LocalPlaybackUrl.ToFilesystemPath("/data/user/0/com.k7/files/a.mp4?startSeconds=90")
            .Should().Be("/data/user/0/com.k7/files/a.mp4");
    }

    [Test]
    public void CreateFileUri_ShouldBeAbsoluteFileScheme()
    {
        var temp = Path.GetTempFileName();
        try
        {
            var uri = LocalPlaybackUrl.CreateFileUri(temp);

            uri.IsAbsoluteUri.Should().BeTrue();
            uri.Scheme.Should().Be(Uri.UriSchemeFile);
            uri.IsFile.Should().BeTrue();
            Path.GetFullPath(uri.LocalPath).Should().Be(Path.GetFullPath(temp));
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Test]
    public void CreateFileUri_ShouldNotThrow_WhenPathIsUnixStyle()
    {
        var uri = LocalPlaybackUrl.CreateFileUri("/data/user/0/com.k7/files/downloads/movies/a.mp4");

        uri.IsAbsoluteUri.Should().BeTrue();
        uri.Scheme.Should().Be(Uri.UriSchemeFile);
    }

    [Test]
    public void TryGetLocalFilesystemPath_ShouldReturnFalse_ForHttp()
    {
        LocalPlaybackUrl.TryGetLocalFilesystemPath("https://host/manifest.m3u8", out var path)
            .Should().BeFalse();
        path.Should().BeNull();
    }
}
