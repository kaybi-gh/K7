using K7.Server.Infrastructure.MediaProcessing;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common;

public class FfmpegVideoEncoderBuilderTests
{
    [Test]
    public void Resolve_ShouldPreferVerifiedHardwareEncoder_WhenAuto()
    {
        var settings = new TranscodeSettingsDto { EncoderMode = HardwareEncoderMode.Auto };
        var capabilities = new FfmpegCapabilitiesDto
        {
            AvailableHardwareEncoders = ["h264_vaapi"]
        };

        var selection = FfmpegVideoEncoderBuilder.Resolve("h264", settings, capabilities);

        selection.Should().NotBeNull();
        selection!.EncoderName.Should().Be("h264_vaapi");
        selection.IsHardwareAccelerated.Should().BeTrue();
    }

    [Test]
    public void Resolve_ShouldUseSoftware_WhenNoHardwareVerifiedAndAuto()
    {
        var settings = new TranscodeSettingsDto { EncoderMode = HardwareEncoderMode.Auto };
        var capabilities = new FfmpegCapabilitiesDto
        {
            AvailableHardwareEncoders = []
        };

        var selection = FfmpegVideoEncoderBuilder.Resolve("h264", settings, capabilities);

        selection.Should().NotBeNull();
        selection!.EncoderName.Should().Be("libx264");
        selection.IsHardwareAccelerated.Should().BeFalse();
    }

    [Test]
    public void Resolve_ShouldIgnoreUnverifiedNvenc_WhenOnlyVaapiVerified()
    {
        var settings = new TranscodeSettingsDto { EncoderMode = HardwareEncoderMode.Auto };
        var capabilities = new FfmpegCapabilitiesDto
        {
            AvailableHardwareEncoders = ["h264_vaapi"]
        };

        var selection = FfmpegVideoEncoderBuilder.Resolve("h264", settings, capabilities);

        selection!.EncoderName.Should().Be("h264_vaapi");
    }

    [Test]
    public void Resolve_ShouldForceSoftware_WhenModeIsSoftware()
    {
        var settings = new TranscodeSettingsDto { EncoderMode = HardwareEncoderMode.Software };
        var capabilities = new FfmpegCapabilitiesDto
        {
            AvailableHardwareEncoders = ["h264_nvenc", "h264_vaapi"]
        };

        var selection = FfmpegVideoEncoderBuilder.Resolve("h264", settings, capabilities);

        selection!.EncoderName.Should().Be("libx264");
    }

    [Test]
    public void CreateHardwareSelection_ShouldReturnNull_WhenUnknownEncoder()
    {
        FfmpegVideoEncoderBuilder.CreateHardwareSelection("libx264").Should().BeNull();
    }

    [Test]
    public void CreateHardwareSelection_ShouldIncludeVaapiDeviceInit()
    {
        var selection = FfmpegVideoEncoderBuilder.CreateHardwareSelection("h264_vaapi");

        selection.Should().NotBeNull();
        selection!.GlobalArguments.Should().Contain("-init_hw_device vaapi=");
        selection.GlobalArguments.Should().Contain("-filter_hw_device va");
        selection.EncoderArguments.Should().Be("-c:v h264_vaapi");
        selection.VideoFilter.Should().Be("format=nv12,hwupload");
        selection.UsesHardwareDecode.Should().BeFalse();
    }

    [Test]
    public void GetHdrTonemapFilter_ShouldIncludeLinearPrimariesChain_WhenEnabled()
    {
        var filter = FfmpegVideoEncoderBuilder.GetHdrTonemapFilter(true);

        filter.Should().Contain("npl=100");
        filter.Should().Contain("format=gbrpf32le");
        filter.Should().Contain("primaries=bt709");
        filter.Should().Contain("tonemap=hable");
        FfmpegVideoEncoderBuilder.GetHdrTonemapFilter(false).Should().BeNull();
    }

    [Test]
    public void BuildVideoFilterChain_ShouldOrderTonemapScaleThenEncoderFilter()
    {
        var filter = FfmpegVideoEncoderBuilder.BuildVideoFilterChain(
            "zscale=transfer=linear",
            720,
            "format=nv12,hwupload");

        filter.Should().Be("zscale=transfer=linear,scale=-2:720,format=nv12,hwupload");
    }

    [Test]
    public void IsHdrTransfer_ShouldDetectHdr10AndHlg()
    {
        HdrVideoProbe.IsHdrTransfer("smpte2084").Should().BeTrue();
        HdrVideoProbe.IsHdrTransfer("arib-std-b67").Should().BeTrue();
        HdrVideoProbe.IsHdrTransfer("bt709").Should().BeFalse();
        HdrVideoProbe.IsHdrTransfer(null).Should().BeFalse();
    }
}
