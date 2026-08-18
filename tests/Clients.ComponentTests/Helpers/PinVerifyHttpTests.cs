using K7.Shared;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PinVerifyHttpTests
{
    [Test]
    public void IsPinVerifyRequest_ShouldReturnTrue_ForSharedProfileAbsoluteUri()
    {
        var uri = new Uri("https://k7.local/api/shared-profiles/11111111-1111-1111-1111-111111111111/verify-pin");

        PinVerifyHttp.IsPinVerifyRequest(uri).Should().BeTrue();
    }

    [Test]
    public void IsPinVerifyRequest_ShouldReturnTrue_ForUserRelativeUri()
    {
        var uri = new Uri("api/users/11111111-1111-1111-1111-111111111111/verify-pin", UriKind.Relative);

        PinVerifyHttp.IsPinVerifyRequest(uri).Should().BeTrue();
    }

    [Test]
    public void IsPinVerifyRequest_ShouldReturnFalse_ForOtherApi()
    {
        var uri = new Uri("https://k7.local/api/shared-profiles");

        PinVerifyHttp.IsPinVerifyRequest(uri).Should().BeFalse();
    }

    [Test]
    public void IsPinVerifyRequest_ShouldReturnFalse_WhenUriIsNull()
    {
        PinVerifyHttp.IsPinVerifyRequest(null).Should().BeFalse();
    }
}
