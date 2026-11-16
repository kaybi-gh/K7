using K7.Server.Infrastructure.MediaProcessing;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

/// <summary>
/// Quality and codec names come from the request and name the transcode output directory,
/// where files are deleted and created. They must never be able to leave the transcoding folder.
/// </summary>
[TestFixture]
public class TranscodeJobManagerOutputPathTests
{
    [TestCase(null)]
    [TestCase("original")]
    [TestCase("1080p")]
    [TestCase("h264")]
    [TestCase("hevc")]
    [TestCase("libx264_main")]
    public void EnsureSafeToken_ShouldAccept_WhenValueIsAPlainToken(string? value)
    {
        var act = () => TranscodeJobManager.EnsureSafeToken(value, "videoCodec");

        act.Should().NotThrow();
    }

    [TestCase("")]
    [TestCase("..")]
    [TestCase("x/../../../etc")]
    [TestCase("x\\..\\..\\windows")]
    [TestCase("a b")]
    [TestCase("h264;rm")]
    [TestCase("C:")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void EnsureSafeToken_ShouldThrowValidation_WhenValueCouldChangeThePath(string value)
    {
        var act = () => TranscodeJobManager.EnsureSafeToken(value, "videoCodec");

        act.Should().Throw<ValidationException>();
    }

    [Test]
    public void EnsureInsideRoot_ShouldAccept_WhenDirectoryIsUnderTheRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-transcoding");

        var act = () => TranscodeJobManager.EnsureInsideRoot(Path.Combine(root, "file", "video-original-copy"), root);

        act.Should().NotThrow();
    }

    [Test]
    public void EnsureInsideRoot_ShouldThrow_WhenDirectoryEscapesTheRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-transcoding");

        var act = () => TranscodeJobManager.EnsureInsideRoot(Path.Combine(root, "file", "..", "..", "elsewhere"), root);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void EnsureInsideRoot_ShouldThrow_WhenDirectoryOnlySharesThePrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-transcoding");

        var act = () => TranscodeJobManager.EnsureInsideRoot(root + "-other", root);

        act.Should().Throw<InvalidOperationException>();
    }
}
