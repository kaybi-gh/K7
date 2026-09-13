using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class FfmpegCapabilitiesServiceTests
{
    [Test]
    public void HasFilter_ShouldBeTrue_WhenScaleCudaListed()
    {
        const string output =
            """
            Filters:
             ... scale               V->V       Scale the input video size
             TSC scale_cuda          V->V       GPU accelerated video resizer
             ... hwupload            V->V       Upload a system frame to a hw frame
            """;

        FfmpegCapabilitiesService.HasFilter(output, "scale_cuda").Should().BeTrue();
        FfmpegCapabilitiesService.HasFilter(output, "scale").Should().BeTrue();
    }

    [Test]
    public void HasFilter_ShouldBeFalse_WhenFilterMissing()
    {
        const string output =
            """
            Filters:
             ... scale               V->V       Scale the input video size
             ... hwupload            V->V       Upload a system frame to a hw frame
            """;

        FfmpegCapabilitiesService.HasFilter(output, "scale_cuda").Should().BeFalse();
    }

    [Test]
    public void HasFilter_ShouldIgnoreHeaderLines()
    {
        const string output =
            """
            Filters:
              T.. = Timeline support
              .S. = Slice threading
             ... scale               V->V       Scale the input video size
            """;

        FfmpegCapabilitiesService.HasFilter(output, "Filters").Should().BeFalse();
        FfmpegCapabilitiesService.HasFilter(output, "Timeline").Should().BeFalse();
        FfmpegCapabilitiesService.HasFilter(output, "scale").Should().BeTrue();
    }
}
