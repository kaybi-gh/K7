using FFMpegCore;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegStreamingInputTests
{
    [Test]
    public void Create_ShouldPlaceSeekBeforeInput_WhenLocalFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"k7-ffmpeg-input-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(tempFile, [0x00]);

        try
        {
            var input = FfmpegMediaInput.FromFile(tempFile);
            var args = FfmpegStreamingInput.Create(input, options =>
            {
                options.WithCustomArgument("-ss 12.5");
            });

            var text = args.Text;
            var seekIndex = text.IndexOf("-ss 12.5", StringComparison.Ordinal);
            var inputIndex = text.IndexOf(tempFile, StringComparison.Ordinal);

            seekIndex.Should().BeGreaterThanOrEqualTo(0);
            inputIndex.Should().BeGreaterThan(seekIndex);
            text.Should().NotContain("-headers");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
