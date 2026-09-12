using K7.Server.Application.Features.Notifications;

namespace K7.Server.Application.UnitTests.Features.Notifications;

[TestFixture]
public class NotificationWebhookUrlTests
{
    [Test]
    public void IsHttpOrHttps_ShouldAcceptHttpAndHttps()
    {
        NotificationWebhookUrl.IsHttpOrHttps("https://hooks.example/x").Should().BeTrue();
        NotificationWebhookUrl.IsHttpOrHttps("http://localhost:8080/hook").Should().BeTrue();
    }

    [Test]
    public void IsHttpOrHttps_ShouldRejectNonHttpSchemesAndEmpty()
    {
        NotificationWebhookUrl.IsHttpOrHttps(null).Should().BeFalse();
        NotificationWebhookUrl.IsHttpOrHttps("").Should().BeFalse();
        NotificationWebhookUrl.IsHttpOrHttps("ftp://example/x").Should().BeFalse();
        NotificationWebhookUrl.IsHttpOrHttps("not-a-url").Should().BeFalse();
    }

    [Test]
    public void ProviderConfigHasHttpUrl_ShouldReadUrlProperty()
    {
        NotificationWebhookUrl.ProviderConfigHasHttpUrl("""{"url":"https://example/hook"}""")
            .Should().BeTrue();
        NotificationWebhookUrl.ProviderConfigHasHttpUrl("""{"url":"ftp://bad"}""")
            .Should().BeFalse();
        NotificationWebhookUrl.ProviderConfigHasHttpUrl("""{"method":"POST"}""")
            .Should().BeFalse();
        NotificationWebhookUrl.ProviderConfigHasHttpUrl("not-json")
            .Should().BeFalse();
    }
}
