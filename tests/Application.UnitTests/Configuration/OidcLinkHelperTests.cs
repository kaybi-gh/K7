using K7.Server.Infrastructure.Configuration;

namespace K7.Server.Application.UnitTests.Configuration;

[TestFixture]
public class OidcLinkHelperTests
{
    [Test]
    public void IsLinkRequest_ShouldDetectMarkerInItems()
    {
        var items = new Dictionary<string, string?>
        {
            [OidcLinkHelper.LinkMarkerKey] = OidcLinkHelper.LinkMarkerValue
        };

        OidcLinkHelper.IsLinkRequest(items, "/settings/account").Should().BeTrue();
        OidcLinkHelper.IsLinkRequest(new Dictionary<string, string?>(), "/settings/account").Should().BeFalse();
        OidcLinkHelper.IsLinkRequest(null, "/settings/account").Should().BeFalse();
    }

    [Test]
    public void IsLinkRequest_ShouldDetectPendingQueryInReturnUrl()
    {
        OidcLinkHelper.IsLinkRequest(null, "/settings/account?oidcLinkPending=1").Should().BeTrue();
        OidcLinkHelper.IsLinkRequest(null, "/settings/account").Should().BeFalse();
    }

    [Test]
    public void BuildPendingUrl_ShouldAppendPendingMarkerAndRejectOpenRedirects()
    {
        OidcLinkHelper.BuildPendingUrl("/settings/account")
            .Should().Be("/settings/account?oidcLinkPending=1");

        OidcLinkHelper.BuildPendingUrl("/settings/account?tab=security")
            .Should().Be("/settings/account?tab=security&oidcLinkPending=1");

        OidcLinkHelper.BuildPendingUrl("https://evil.example/phish")
            .Should().Be("/settings/account?oidcLinkPending=1");
    }

    [Test]
    public void BuildResultUrl_ShouldReplacePendingWithResult()
    {
        OidcLinkHelper.BuildResultUrl("/settings/account?oidcLinkPending=1", "success")
            .Should().Be("/settings/account?oidcLink=success");

        OidcLinkHelper.BuildResultUrl("/settings/account?oidcLinkPending=1", "conflict")
            .Should().Be("/settings/account?oidcLink=conflict");

        OidcLinkHelper.BuildResultUrl("/settings/account?oidcLinkPending=1", "already_linked")
            .Should().Be("/settings/account?oidcLink=already_linked");
    }

    [Test]
    public void IsSafeLocalUrl_ShouldOnlyAllowRootRelativePaths()
    {
        OidcLinkHelper.IsSafeLocalUrl("/settings/account").Should().BeTrue();
        OidcLinkHelper.IsSafeLocalUrl("//evil.example").Should().BeFalse();
        OidcLinkHelper.IsSafeLocalUrl("https://evil.example").Should().BeFalse();
        OidcLinkHelper.IsSafeLocalUrl(null).Should().BeFalse();
    }
}
