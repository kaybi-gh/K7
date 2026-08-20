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
    public void CreateHardwareSelection_ShouldDisableNvencLookahead()
    {
        var selection = FfmpegVideoEncoderBuilder.CreateHardwareSelection("h264_nvenc");

        selection.Should().NotBeNull();
        selection!.EncoderArguments.Should().Contain("-zerolatency 1");
        selection.EncoderArguments.Should().Contain("-rc-lookahead 0");
        selection.EncoderArguments.Should().Contain("-no-scenecut 1");
        selection.EncoderArguments.Should().Contain("-rc vbr");
        selection.DecodeArguments.Should().Be("-hwaccel cuda -hwaccel_output_format cuda");
        selection.VideoFilter.Should().Be("format=nv12|cuda,hwupload,scale_cuda=format=nv12");
        selection.HardwareScaleFilterTemplate.Should().Be(
            "format=nv12|cuda,hwupload,scale_cuda={0}:{1}:format=nv12");
    }

    [Test]
    public void BuildVideoFilterChain_ShouldUseCudaScale_WhenNvencAndNoHdr()
    {
        var selection = FfmpegVideoEncoderBuilder.CreateHardwareSelection("h264_nvenc");

        var filter = FfmpegVideoEncoderBuilder.BuildVideoFilterChain(
            hdrTonemapFilter: null,
            scaleHeight: 720,
            encoderVideoFilter: selection!.VideoFilter,
            hardwareScaleFilterTemplate: selection.HardwareScaleFilterTemplate,
            scaleWidth: 1280);

        filter.Should().Be("format=nv12|cuda,hwupload,scale_cuda=1280:720:format=nv12");
        filter.Should().NotContain("scale=-2:");
    }

    [Test]
    public void BuildVideoFilterChain_ShouldKeepCpuScale_WhenHdrTonemap()
    {
        var selection = FfmpegVideoEncoderBuilder.CreateHardwareSelection("h264_nvenc");

        var filter = FfmpegVideoEncoderBuilder.BuildVideoFilterChain(
            "zscale=transfer=linear",
            720,
            selection!.VideoFilter,
            selection.HardwareScaleFilterTemplate,
            1280);

        filter.Should().Be(
            "zscale=transfer=linear,scale=-2:720,format=nv12|cuda,hwupload,scale_cuda=format=nv12");
    }

    [Test]
    public void CreateHardwareSelection_ShouldNotUseDxva2Decode_ForAmf()
    {
        var selection = FfmpegVideoEncoderBuilder.CreateHardwareSelection("h264_amf");

        selection.Should().NotBeNull();
        selection!.EncoderName.Should().Be("h264_amf");
        selection.IsHardwareAccelerated.Should().BeTrue();
        selection.UsesHardwareDecode.Should().BeFalse();
        selection.DecodeArguments.Should().BeNull();
        selection.EncoderArguments.Should().Contain("-c:v h264_amf");
        selection.EncoderArguments.Should().Contain("-forced_idr 1");
    }

    [Test]
    public void BuildQualityBitrateArguments_ShouldConstrainVbvToLadder()
    {
        var args = FfmpegVideoEncoderBuilder.BuildQualityBitrateArguments(2_800_000, 3_500_000);

        args.Should().Equal("-b:v 2800000", "-maxrate 3500000", "-bufsize 17500000");
    }
}
