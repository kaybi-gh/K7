using System.Net;
using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
[NonParallelizable]
public class PerfTraceTests
{
    private List<string> _lines = null!;

    [SetUp]
    public void SetUp()
    {
        _lines = [];
        PerfTrace.Sink = _lines.Add;
    }

    [TearDown]
    public void TearDown()
    {
        PerfTrace.SetEnabled(false);
        PerfTrace.Sink = null;
    }

    [Test]
    public void Mark_ShouldWriteNothing_WhenDisabled()
    {
        PerfTrace.NavigationStarted("https://app/movies/1");
        PerfTrace.Mark("Movie", "ready");
        PerfTrace.Request("GET", "/api/medias/1", 200, TimeSpan.FromMilliseconds(10), 100);

        _lines.Should().BeEmpty();
    }

    [Test]
    public void Mark_ShouldWriteOncePerNavigation_WhenEnabled()
    {
        PerfTrace.SetEnabled(true);
        PerfTrace.NavigationStarted("https://app/movies/1");
        PerfTrace.Mark("Movie", "ready");
        PerfTrace.Mark("Movie", "ready");
        PerfTrace.NavigationStarted("https://app/movies/2");
        PerfTrace.Mark("Movie", "ready");

        _lines.Should().HaveCount(5);
        _lines[1].Should().MatchRegex(@"^nav#\d+ /movies/1$");
        _lines[2].Should().MatchRegex(@"^nav#\d+ Movie ready \+\d+ms$");
        _lines[3].Should().EndWith(" /movies/2");
        _lines[4].Should().Contain("Movie ready");
    }

    [Test]
    public void Note_ShouldWriteEveryTime_WhenEnabled()
    {
        PerfTrace.SetEnabled(true);
        PerfTrace.Note("select-user start");
        PerfTrace.Note("select-user start");

        _lines.Count(l => l.Contains(" select-user start +")).Should().Be(2);
    }

    [Test]
    public async Task Handler_ShouldLogTheCall_WhenEnabled()
    {
        PerfTrace.SetEnabled(true);
        using var client = new HttpClient(new PerfTraceHttpHandler { InnerHandler = new StubHandler() })
        {
            BaseAddress = new Uri("https://server/")
        };

        using var response = await client.GetAsync("api/medias/1/next-episode");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _lines.Should().ContainSingle(l => l.Contains(" http GET /api/medias/1/next-episode 200 "));
    }

    [Test]
    public async Task Handler_ShouldStaySilent_WhenDisabled()
    {
        using var client = new HttpClient(new PerfTraceHttpHandler { InnerHandler = new StubHandler() })
        {
            BaseAddress = new Uri("https://server/")
        };

        using var response = await client.GetAsync("api/medias/1");

        _lines.Should().BeEmpty();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}
