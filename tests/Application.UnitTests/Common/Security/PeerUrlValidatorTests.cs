using K7.Server.Application.Common.Security;

namespace K7.Server.Application.UnitTests.Common.Security;

[TestFixture]
public class PeerUrlValidatorTests
{
    [Test]
    public void ValidateOutgoingUrl_ShouldRejectNonAbsoluteUrl()
    {
        var act = () => PeerUrlValidator.ValidateOutgoingUrl(
            "not-a-url",
            new PeerUrlValidationOptions { AllowInsecureHttp = false, BlockPrivateNetworks = false });

        act.Should().Throw<InvalidOperationException>().WithMessage("*invalid*");
    }

    [Test]
    public void ValidateOutgoingUrl_ShouldRejectHttpWhenHttpsRequired()
    {
        var act = () => PeerUrlValidator.ValidateOutgoingUrl(
            "http://peer.example",
            new PeerUrlValidationOptions { AllowInsecureHttp = false, BlockPrivateNetworks = false });

        act.Should().Throw<InvalidOperationException>().WithMessage("*HTTPS*");
    }

    [Test]
    public void ValidateOutgoingUrl_ShouldRejectLoopback()
    {
        var act = () => PeerUrlValidator.ValidateOutgoingUrl(
            "https://localhost",
            new PeerUrlValidationOptions { AllowInsecureHttp = false, BlockPrivateNetworks = false });

        act.Should().Throw<InvalidOperationException>().WithMessage("*loopback*");
    }

    [Test]
    public void ValidateOutgoingUrl_ShouldRejectCloudMetadataHost()
    {
        var act = () => PeerUrlValidator.ValidateOutgoingUrl(
            "https://metadata.google.internal",
            new PeerUrlValidationOptions { AllowInsecureHttp = false, BlockPrivateNetworks = false });

        act.Should().Throw<InvalidOperationException>().WithMessage("*metadata*");
    }
}
