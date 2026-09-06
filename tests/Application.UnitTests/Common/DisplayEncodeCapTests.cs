using K7.Server.Application.Common;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Common;

public class DisplayEncodeCapTests
{
    [Test]
    public void TryGetEncodeQuality_ShouldReturnNull_WhenResolutionMissing()
    {
        var device = new Device
        {
            ClientType = ClientType.Web,
            DisplayScreenHeight = 1080,
            DisplayResolutionHeight = 0
        };

        DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160).Should().BeNull();
    }

    [Test]
    public void TryGetEncodeQuality_ShouldIgnoreLogicalScreenSize()
    {
        var device = new Device
        {
            ClientType = ClientType.Web,
            DisplayScreenHeight = 720,
            DisplayResolutionHeight = 2160
        };

        DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160).Should().BeNull();
    }

    [Test]
    public void TryGetEncodeQuality_ShouldReturnNull_WhenSourceFitsResolution()
    {
        var device = new Device { ClientType = ClientType.Web, DisplayResolutionHeight = 1080 };

        DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 1080).Should().BeNull();
    }

    [Test]
    public void TryGetEncodeQuality_ShouldPick1080p_When4KOn1080pDisplay()
    {
        var device = new Device { ClientType = ClientType.Web, DisplayResolutionHeight = 1080 };

        var quality = DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160);

        quality.Should().NotBeNull();
        quality!.Name.Should().Be("1080p");
        quality.Height.Should().Be(1080);
    }

    [Test]
    public void TryGetEncodeQuality_ShouldCapNativeEncode_WhenResolutionIsPhysical()
    {
        var device = new Device
        {
            ClientType = ClientType.Native,
            DisplayScreenHeight = 720,
            DisplayResolutionHeight = 1080
        };

        var quality = DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160);

        quality.Should().NotBeNull();
        quality!.Name.Should().Be("1080p");
    }

    [Test]
    public void TryGetEncodeQuality_ShouldPick720p_WhenDisplayIsBetweenRungs()
    {
        var device = new Device { ClientType = ClientType.Web, DisplayResolutionHeight = 900 };

        var quality = DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160);

        quality.Should().NotBeNull();
        quality!.Name.Should().Be("720p");
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepExplicitLadderRung()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.QualityDownscale,
            StreamResolution = "1920x1080"
        };

        DisplayEncodeCap.ResolveJobQuality("720p", decision).Should().Be("720p");
    }

    [Test]
    public void ResolveJobQuality_ShouldUseDisplayCap_WhenOriginalAndDecisionCapped()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.VideoCodecNotSupported | TranscodeReason.QualityDownscale,
            StreamResolution = "1920x1080"
        };

        DisplayEncodeCap.ResolveJobQuality("original", decision).Should().Be("1080p");
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepOriginal_WhenNoCap()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.VideoCodecNotSupported,
            StreamResolution = "3840x2160"
        };

        DisplayEncodeCap.ResolveJobQuality("original", decision).Should().Be("original");
    }

    [Test]
    public void TryGetEncodeQuality_ShouldReturnNull_WhenSourceHeightIsZero()
    {
        var device = new Device { ClientType = ClientType.Web, DisplayResolutionHeight = 1080 };

        DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 0).Should().BeNull();
    }

    [Test]
    public void TryGetEncodeQuality_ShouldReturnNull_WhenResolutionIsBelowLadder()
    {
        var device = new Device { ClientType = ClientType.Web, DisplayResolutionHeight = 100 };

        DisplayEncodeCap.TryGetEncodeQuality(device, sourceHeight: 2160).Should().BeNull();
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepOriginal_WhenDecisionIsNull()
    {
        DisplayEncodeCap.ResolveJobQuality("original", decision: null).Should().Be("original");
        DisplayEncodeCap.ResolveJobQuality(null, decision: null).Should().Be("original");
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepOriginal_WhenStreamResolutionIsInvalid()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.QualityDownscale,
            StreamResolution = "not-a-size"
        };

        DisplayEncodeCap.ResolveJobQuality("original", decision).Should().Be("original");
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepOriginal_WhenStreamResolutionIsNotALadderRung()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.QualityDownscale,
            StreamResolution = "100x80"
        };

        DisplayEncodeCap.ResolveJobQuality("original", decision).Should().Be("original");
    }

    [Test]
    public void ResolveJobQuality_ShouldKeepOriginal_WhenQualityDownscaleHasNoStreamResolution()
    {
        var decision = new StreamDecisionDto
        {
            Reason = TranscodeReason.QualityDownscale,
            StreamResolution = null
        };

        DisplayEncodeCap.ResolveJobQuality("original", decision).Should().Be("original");
    }
}
