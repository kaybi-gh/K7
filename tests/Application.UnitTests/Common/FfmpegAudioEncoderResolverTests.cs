using K7.Server.Application.Common;

namespace K7.Server.Application.UnitTests.Common;

public class FfmpegAudioEncoderResolverTests
{
    [Test]
    public void ResolveAacEncoder_ShouldPreferLibfdk_WhenAvailable()
    {
        var encoder = FfmpegAudioEncoderResolver.ResolveAacEncoder(["aac", "libfdk_aac", "libx264"]);

        encoder.Should().Be("libfdk_aac");
    }

    [Test]
    public void ResolveAacEncoder_ShouldFallBackToAac_WhenLibfdkMissing()
    {
        var encoder = FfmpegAudioEncoderResolver.ResolveAacEncoder(["aac", "libx264"]);

        encoder.Should().Be("aac");
    }

    [Test]
    public void BuildAacEncodeArguments_ShouldUseFixed256kStereoForHls()
    {
        var args = FfmpegAudioEncoderResolver.BuildAacEncodeArguments(
            "aac",
            forceChannels: FfmpegAudioEncoderResolver.HlsStereoChannels,
            sampleRateHz: FfmpegAudioEncoderResolver.DefaultSampleRateHz);

        args.Should().Equal(
            "-c:a aac",
            "-ac 2",
            "-ar 48000",
            "-b:a 256000");
    }

    [Test]
    public void BuildAacEncodeArguments_ShouldUseLibfdkVbr_WhenPreferred()
    {
        var args = FfmpegAudioEncoderResolver.BuildAacEncodeArguments(
            "libfdk_aac",
            forceChannels: 2,
            sampleRateHz: 48000);

        args.Should().Equal(
            "-c:a libfdk_aac",
            "-ac 2",
            "-ar 48000",
            "-vbr:a 5");
    }

    [Test]
    public void ResolveEncoderName_ShouldMapOpusToLibopus()
    {
        FfmpegAudioEncoderResolver.ResolveEncoderName("opus").Should().Be("libopus");
    }
}
