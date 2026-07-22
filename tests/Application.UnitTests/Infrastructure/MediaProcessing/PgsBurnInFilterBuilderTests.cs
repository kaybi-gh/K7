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
    public void ResolveDimensions_ShouldUseVideoSize_WhenSubtitleUnspecified()
    {
        var dims = PgsBurnInFilterBuilder.ResolveDimensions(1920, 1080, 0, 0);

        dims.Should().Be((1920, 1080, 1920, 1080));
    }

    [Test]
    public void BuildFilterComplex_ShouldUseScale2ref_WhenAspectRatiosMatch()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(2, 1920, 1080, 1920, 1080);

        filter.Should().Contain("scale2ref");
        filter.Should().Contain("[0:2]");
        filter.Should().NotContain("pad=");
        filter.Should().EndWith("[vout]");
        filter.Should().NotContain("[burned]");
    }

    [Test]
    public void BuildFilterComplex_ShouldPadVideo_WhenSubtitleCanvasIsTaller()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(1, 1920, 800, 1920, 1080);

        filter.Should().Contain("pad=1920:1080");
        filter.Should().Contain("[0:1]");
    }

    [Test]
    public void BuildFilterComplex_ShouldUseVideoCanvas_WhenSubtitleSizeIsZero()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(2, 1920, 1080, 0, 0);

        filter.Should().Contain("scale2ref");
        filter.Should().NotContain("pad=");
    }

    [Test]
    public void BuildFilterComplex_ShouldAppendScaleInGraph_WhenScaleHeightProvided()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(2, 1920, 1080, 1920, 1080, scaleHeight: 360);

        filter.Should().Contain("scale2ref");
        filter.Should().Contain("[burned]");
        filter.Should().Contain("scale=trunc(oh*a/2)*2:360");
        filter.Should().EndWith("[vout]");
        filter.Should().Be(
            "[0:v:0][0:2]scale2ref=flags=lanczos[base][sub];" +
            "[base][sub]overlay=format=auto:eof_action=pass:repeatlast=0[burned];" +
            "[burned]scale=trunc(oh*a/2)*2:360[vout]");
    }

    [Test]
    public void BuildFilterComplex_ShouldAppendScaleAndExtraFilter_WhenBothProvided()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(
            2,
            1920,
            1080,
            1920,
            1080,
            scaleHeight: 720,
            additionalVideoFilter: "format=nv12,hwupload");

        filter.Should().Contain("[burned]scale=trunc(oh*a/2)*2:720,format=nv12,hwupload[vout]");
    }

    [Test]
    public void BuildFilterComplex_ShouldAppendVaapiHwuploadOnly_WhenNoScale()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(
            2,
            1920,
            1080,
            1920,
            1080,
            additionalVideoFilter: "format=nv12,hwupload");

        filter.Should().Be(
            "[0:v:0][0:2]scale2ref=flags=lanczos[base][sub];" +
            "[base][sub]overlay=format=auto:eof_action=pass:repeatlast=0[burned];" +
            "[burned]format=nv12,hwupload[vout]");
    }

    [Test]
    public void BuildFilterComplex_ShouldAppendScaleAfterPadOverlay_WhenCanvasMismatch()
    {
        var filter = PgsBurnInFilterBuilder.BuildFilterComplex(1, 1920, 800, 1920, 1080, scaleHeight: 480);

        filter.Should().Contain("pad=1920:1080");
        filter.Should().Contain("[burned]scale=trunc(oh*a/2)*2:480[vout]");
    }
}
