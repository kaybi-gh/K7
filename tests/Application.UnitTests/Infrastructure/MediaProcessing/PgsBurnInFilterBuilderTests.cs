using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class PgsBurnInFilterBuilderTests
{
    [Test]
    public void BuildCanvasSizeArgument_ShouldReturnNull_WhenInvalid()
    {
        PgsBurnInFilterBuilder.BuildCanvasSizeArgument(0, 1080).Should().BeNull();
        PgsBurnInFilterBuilder.BuildCanvasSizeArgument(1920, -1).Should().BeNull();
    }

    [Test]
    public void BuildCanvasSizeArgument_ShouldFormatDimensions()
    {
        PgsBurnInFilterBuilder.BuildCanvasSizeArgument(1920, 1080).Should().Be("-canvas_size 1920x1080");
    }

    [Test]
    public void BuildFilterComplex_ShouldUseScale2ref_WhenAspectRatiosMatch()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(2, 1920, 1080, 1920, 1080);

        filter.Should().Contain("scale2ref");
        filter.Should().Contain("[0:2]");
        filter.Should().NotContain("pad=");
    }

    [Test]
    public void BuildFilterComplex_ShouldPadVideo_WhenSubtitleCanvasIsTaller()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(1, 1920, 800, 1920, 1080);

        filter.Should().Contain("pad=1920:1080");
        filter.Should().Contain("[0:1]");
    }
}
