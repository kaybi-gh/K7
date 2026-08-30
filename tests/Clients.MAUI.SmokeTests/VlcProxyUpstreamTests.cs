using System.Net;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class VlcProxyUpstreamTests
{
    [Test]
    public async Task SendAsync_ShouldRetry503UntilSuccess()
    {
        var handler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        using var response = await VlcProxyUpstream.SendAsync(
            client,
            "GET",
            "http://127.0.0.1/hls/0.m4s",
            range: null,
            CancellationToken.None,
            (_, _) => Task.CompletedTask);

        ((int)response.StatusCode).Should().Be(200);
        handler.SendCount.Should().Be(3);
    }

    [Test]
    public async Task SendAsync_ShouldStopAfterMax503Retries()
    {
        var handler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        using var response = await VlcProxyUpstream.SendAsync(
            client,
            "GET",
            "http://127.0.0.1/hls/init.m4s",
            range: null,
            CancellationToken.None,
            (_, _) => Task.CompletedTask);

        ((int)response.StatusCode).Should().Be(503);
        handler.SendCount.Should().Be(VlcProxyUpstream.ServiceUnavailableRetries + 1);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _codes;

        public SequenceHandler(params HttpStatusCode[] codes) =>
            _codes = new Queue<HttpStatusCode>(codes);

        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            var code = _codes.Count > 0 ? _codes.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(code == HttpStatusCode.OK ? "ok" : "busy")
            });
        }
    }
}
