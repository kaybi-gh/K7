using System.Net;
using K7.Server.Application.Features.Scrobbling.Services;

namespace K7.Server.Application.UnitTests.Features.Scrobbling;

[TestFixture]
public class ScrobbleRateLimitTests
{
    [Test]
    public void TryGetDelay_ShouldPreferRetryAfterDelta()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));

        var delay = ScrobbleRateLimit.TryGetDelay(response, TimeSpan.FromSeconds(60));

        delay.Should().Be(TimeSpan.FromSeconds(12));
    }

    [Test]
    public void TryGetDelay_ShouldUseResetInHeader()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("X-RateLimit-Reset-In", "8");

        var delay = ScrobbleRateLimit.TryGetDelay(response, TimeSpan.FromSeconds(60));

        delay.Should().Be(TimeSpan.FromSeconds(8));
    }

    [Test]
    public void TryGetDelay_ShouldCapDelay()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));

        var delay = ScrobbleRateLimit.TryGetDelay(response, TimeSpan.FromSeconds(60));

        delay.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Test]
    public void TryGetDelay_ShouldBeNull_WhenNot429()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);

        ScrobbleRateLimit.TryGetDelay(response, TimeSpan.FromSeconds(60)).Should().BeNull();
    }
}
