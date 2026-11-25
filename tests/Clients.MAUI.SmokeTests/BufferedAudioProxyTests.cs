using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class BufferedAudioProxyTests
{
    [Test]
    public void TryParseRange_ShouldReturnWholeFile_WhenHeaderIsMissing()
    {
        var ok = BufferedAudioProxy.TryParseRange(10, null, out var start, out var length);

        ok.Should().BeTrue();
        start.Should().Be(0);
        length.Should().Be(10);
    }

    [Test]
    public void TryParseRange_ShouldClampEnd_WhenRangeRunsPastTheFile()
    {
        var ok = BufferedAudioProxy.TryParseRange(10, "bytes=8-99", out var start, out var length);

        ok.Should().BeTrue();
        start.Should().Be(8);
        length.Should().Be(2);
    }

    [Test]
    public void TryParseRange_ShouldUseSuffix_WhenStartIsEmpty()
    {
        var ok = BufferedAudioProxy.TryParseRange(10, "bytes=-3", out var start, out var length);

        ok.Should().BeTrue();
        start.Should().Be(7);
        length.Should().Be(3);
    }

    [Test]
    public void TryParseRange_ShouldReject_WhenStartIsPastTheFile()
    {
        var ok = BufferedAudioProxy.TryParseRange(10, "bytes=20-30", out _, out _);

        ok.Should().BeFalse();
    }

    [Test]
    public async Task Get_ShouldReturnPartialContent_WhenRangeIsSet()
    {
        var data = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        using var proxy = new BufferedAudioProxy(data, "audio/mpeg");
        proxy.TryStart().Should().BeTrue();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, proxy.LocalUrl);
        request.Headers.Range = new RangeHeaderValue(10, 19);
        using var response = await client.SendAsync(request);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.PartialContent);
        response.Headers.AcceptRanges.Should().Contain("bytes");
        response.Content.Headers.ContentRange!.From.Should().Be(10);
        response.Content.Headers.ContentRange.To.Should().Be(19);
        response.Content.Headers.ContentRange.Length.Should().Be(100);
        var body = await response.Content.ReadAsByteArrayAsync();
        body.Should().Equal(data.AsSpan(10, 10).ToArray());
    }

    [Test]
    public async Task Get_ShouldReturnFullBody_WhenRangeIsAbsent()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        using var proxy = new BufferedAudioProxy(data, "audio/flac");
        proxy.TryStart().Should().BeTrue();

        using var client = new HttpClient();
        using var response = await client.GetAsync(proxy.LocalUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/flac");
        response.Content.Headers.ContentLength.Should().Be(4);
        var body = await response.Content.ReadAsByteArrayAsync();
        body.Should().Equal(data);
    }
}
