using K7.Clients.Shared.Services;
using Microsoft.JSInterop;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class JsExternalLinkServiceTests
{
    private IJSRuntime _js = null!;
    private JsExternalLinkService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _js = Substitute.For<IJSRuntime>();
        _sut = new JsExternalLinkService(_js);
    }

    [Test]
    public async Task OpenAsync_ShouldInvokeJs_WhenUrlIsHttps()
    {
        _js.InvokeAsync<bool>("K7.openExternalUrl", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(new ValueTask<bool>(true));

        var opened = await _sut.OpenAsync("https://www.youtube.com/watch?v=abc");

        opened.Should().BeTrue();
        await _js.Received(1).InvokeAsync<bool>(
            "K7.openExternalUrl",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] as string == "https://www.youtube.com/watch?v=abc"));
    }

    [Test]
    public async Task OpenAsync_ShouldReturnFalse_WhenUrlIsNotHttp()
    {
        var opened = await _sut.OpenAsync("javascript:alert(1)");

        opened.Should().BeFalse();
        await _js.DidNotReceiveWithAnyArgs().InvokeAsync<bool>(default!, default, default);
    }

    [Test]
    public async Task OpenAsync_ShouldReturnFalse_WhenJsThrowsDisconnected()
    {
        _js.InvokeAsync<bool>("K7.openExternalUrl", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<bool>(new JSDisconnectedException("disconnected")));

        var opened = await _sut.OpenAsync("https://www.youtube.com/watch?v=abc");

        opened.Should().BeFalse();
    }
}
