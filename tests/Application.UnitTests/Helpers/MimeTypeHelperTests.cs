using K7.Server.Application.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MimeTypeHelperTests
{
    [TestCase(MediaFormatType.Audio, "mp3", "audio/mpeg")]
    [TestCase(MediaFormatType.Audio, "flac", "audio/flac")]
    [TestCase(MediaFormatType.Video, "mkv", "video/x-matroska")]
    [TestCase(MediaFormatType.Video, "mp4", "video/mp4")]
    [TestCase(MediaFormatType.Video, "unknown", "application/octet-stream")]
    public void GetMimeType_ShouldMapContainer(MediaFormatType type, string container, string expected)
    {
        MimeTypeHelper.GetMimeType(type, container).Should().Be(expected);
    }
}
