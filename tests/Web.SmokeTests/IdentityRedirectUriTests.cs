using K7.Server.Web.Components.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class IdentityRedirectUriTests
{
    [Test]
    public void Normalize_ShouldKeepAuthorizeReturnUrl_WhenScopeHasDecodedSpaces()
    {
        var returnUrl =
            "/connect/authorize?client_id=k7-native&redirect_uri=http://localhost:49152/"
            + "&response_type=code&scope=openid email profile roles offline_access api"
            + "&nonce=abc&code_challenge=xyz&code_challenge_method=S256&state=st";

        Uri.IsWellFormedUriString(returnUrl, UriKind.Relative).Should().BeFalse();

        var normalized = IdentityRedirectUri.Normalize(returnUrl, _ => throw new InvalidOperationException(
            "ToBaseRelativePath must not run for local authorize ReturnUrl values."));

        normalized.Should().StartWith("/connect/authorize?");
        normalized.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A49152%2F");
        normalized.Should().Contain("scope=openid%20email%20profile%20roles%20offline_access%20api");
        normalized.Should().NotContain("=http://");
        normalized.Should().NotContain("openid email");
    }

    [Test]
    public void Normalize_ShouldKeepLoopbackAndDeepLinkCallbacks()
    {
        IdentityRedirectUri.Normalize("http://localhost:53333/?code=abc")
            .Should().Be("http://localhost:53333/?code=abc");
        IdentityRedirectUri.Normalize("k7://callback/login?code=abc")
            .Should().Be("k7://callback/login?code=abc");
    }

    [Test]
    public void Normalize_ShouldUseBaseRelativeFallback_WhenUriIsExternal()
    {
        var normalized = IdentityRedirectUri.Normalize(
            "https://evil.example/phish",
            static _ => "safe");

        normalized.Should().Be("safe");
    }

    [Test]
    public void EncodeQuery_ShouldNotDoubleEncodeAlreadyEncodedValues()
    {
        var encoded =
            "/connect/authorize?redirect_uri=http%3A%2F%2Flocalhost%3A49152%2F&scope=openid%20email";

        IdentityRedirectUri.EncodeQuery(encoded).Should().Be(encoded);
    }

    [Test]
    public void Normalize_ShouldSendEmptyUriToHome()
    {
        IdentityRedirectUri.Normalize(null).Should().Be("/");
        IdentityRedirectUri.Normalize("").Should().Be("/");
    }

    [Test]
    public void IsLocalPath_ShouldRejectProtocolRelativeUrls()
    {
        IdentityRedirectUri.IsLocalPath("/connect/authorize").Should().BeTrue();
        IdentityRedirectUri.IsLocalPath("//evil.example").Should().BeFalse();
        IdentityRedirectUri.IsLocalPath("https://evil.example").Should().BeFalse();
    }

    [Test]
    public void Classify_ShouldLabelDestinationsWithoutQuery()
    {
        IdentityRedirectUri.Classify(null).Should().Be("empty");
        IdentityRedirectUri.Classify("").Should().Be("empty");
        IdentityRedirectUri.Classify("/").Should().Be("home");
        IdentityRedirectUri.Classify("/connect/authorize?redirect_uri=http://localhost:9/&code=secret")
            .Should().Be("local-authorize");
        IdentityRedirectUri.Classify("/sign-in").Should().Be("sign-in");
        IdentityRedirectUri.Classify("/welcome").Should().Be("welcome");
        IdentityRedirectUri.Classify("/auth/complete?status=success").Should().Be("auth-complete");
        IdentityRedirectUri.Classify("http://localhost:53333/?code=abc").Should().Be("loopback");
        IdentityRedirectUri.Classify("k7://callback/login?code=abc").Should().Be("custom-scheme");
        IdentityRedirectUri.Classify("https://k7.example/connect/authorize?x=1").Should().Be("local-authorize");
        IdentityRedirectUri.Classify("/library").Should().Be("local-other");
    }

    [Test]
    public void DescribeHost_ShouldIncludeNonDefaultPort()
    {
        IdentityRedirectUri.DescribeHost("http://localhost:49152/").Should().Be("localhost:49152");
        IdentityRedirectUri.DescribeHost("https://k7.example/connect/authorize").Should().Be("k7.example");
        IdentityRedirectUri.DescribeHost("/connect/authorize").Should().Be("-");
    }

    [Test]
    public void Coalesce_ShouldPreferFormThenQueryBindingThenRequest()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["ReturnUrl"] = "/connect/authorize?client_id=k7-native"
        });

        IdentityRedirectUri.Coalesce("/from-form", "/from-binding", query)
            .Should().Be("/from-form");
        IdentityRedirectUri.Coalesce(null, "/from-binding", query)
            .Should().Be("/from-binding");
        IdentityRedirectUri.Coalesce(null, null, query)
            .Should().Be("/connect/authorize?client_id=k7-native");
        IdentityRedirectUri.Coalesce(null, "", query)
            .Should().Be("/connect/authorize?client_id=k7-native");
    }

    [Test]
    public void Coalesce_ShouldReadLowercaseReturnUrlQuery()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["returnUrl"] = "/connect/authorize?client_id=k7-native"
        });

        IdentityRedirectUri.Coalesce(null, null, query)
            .Should().Be("/connect/authorize?client_id=k7-native");
    }

    [Test]
    public void Coalesce_ShouldReturnNull_WhenNothingIsPresent()
    {
        IdentityRedirectUri.Coalesce(null, null, null).Should().BeNull();
        IdentityRedirectUri.Coalesce("", "", new QueryCollection()).Should().BeNull();
    }

    [Test]
    public void RewriteNativeCallbackToLanding_ShouldKeepCodeOnLaunchQuery()
    {
        var rewritten = IdentityRedirectUri.RewriteNativeCallbackToLanding(
            "k7://callback/login?code=abc&state=st");

        rewritten.Should().StartWith("/auth/complete?status=success&launch=");
        var launch = Uri.UnescapeDataString(rewritten!["/auth/complete?status=success&launch=".Length..]);
        launch.Should().Be("k7://callback/login?code=abc&state=st");
    }

    [Test]
    public void RewriteNativeCallbackToLanding_ShouldMapDeniedAndRejectForeignSchemes()
    {
        IdentityRedirectUri.RewriteNativeCallbackToLanding("k7://callback/login?error=access_denied")
            .Should().StartWith("/auth/complete?status=denied&");
        IdentityRedirectUri.RewriteNativeCallbackToLanding("k7://callback/login?error=server_error")
            .Should().StartWith("/auth/complete?status=error&");
        IdentityRedirectUri.RewriteNativeCallbackToLanding("https://evil.example/login").Should().BeNull();
        IdentityRedirectUri.RewriteNativeCallbackToLanding("k7://evil/login?code=abc").Should().BeNull();
        IdentityRedirectUri.RewriteNativeCallbackToLanding("http://localhost:9/?code=abc").Should().BeNull();
    }

    [Test]
    public void BuildNativeLaunchRefresh_ShouldUseHttpRefreshToCustomScheme()
    {
        IdentityRedirectUri.BuildNativeLaunchRefresh(new Uri("k7://callback/login?code=abc"))
            .Should().Be("0; url=k7://callback/login?code=abc");
    }

    [Test]
    public void WantsBrowserLanding_ShouldAcceptDisplayPageOrWindowsUserAgent()
    {
        var display = new DefaultHttpContext();
        display.Request.QueryString = new QueryString("?display=page");
        IdentityRedirectUri.WantsBrowserLanding(display.Request).Should().BeTrue();

        var windows = new DefaultHttpContext();
        windows.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0";
        IdentityRedirectUri.WantsBrowserLanding(windows.Request).Should().BeTrue();

        var android = new DefaultHttpContext();
        android.Request.Headers.UserAgent = "Mozilla/5.0 (Linux; Android 14) Chrome/120.0.0.0";
        IdentityRedirectUri.WantsBrowserLanding(android.Request).Should().BeFalse();
    }
}
