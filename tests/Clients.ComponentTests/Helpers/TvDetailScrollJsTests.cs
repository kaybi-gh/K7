using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TvDetailScrollJsTests
{
    private IJSRuntime _js = null!;

    [SetUp]
    public void SetUp()
    {
        _js = Substitute.For<IJSRuntime>();
    }

    [Test]
    public async Task TryInitAsync_ShouldReturnFalse_WhenElementReferenceIdIsEmpty()
    {
        var initialized = await TvDetailScrollJs.TryInitAsync(_js, default);

        initialized.Should().BeFalse();
        await _js.DidNotReceiveWithAnyArgs().InvokeAsync<bool>(default!, default, default);
    }

    [Test]
    public async Task TryInitAsync_ShouldReturnTrue_WhenJsInitializes()
    {
        var root = new ElementReference("tv-scroll");
        _js.InvokeAsync<bool>("K7.TvDetailScroll.init", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(new ValueTask<bool>(true));

        var initialized = await TvDetailScrollJs.TryInitAsync(_js, root);

        initialized.Should().BeTrue();
        await _js.Received(1).InvokeAsync<bool>(
            "K7.TvDetailScroll.init",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], root)));
    }

    [Test]
    public async Task TryInitAsync_ShouldReturnFalse_WhenJsThrows()
    {
        var root = new ElementReference("tv-scroll");
        _js.InvokeAsync<bool>("K7.TvDetailScroll.init", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<bool>(new JSException("root.addEventListener is not a function")));

        var initialized = await TvDetailScrollJs.TryInitAsync(_js, root);

        initialized.Should().BeFalse();
    }

    [Test]
    public async Task TrySyncAsync_ShouldSkipJs_WhenElementReferenceIdIsEmpty()
    {
        await TvDetailScrollJs.TrySyncAsync(_js, default);

        await _js.DidNotReceiveWithAnyArgs().InvokeAsync<IJSVoidResult>(default!, default, default);
    }

    [Test]
    public async Task TrySyncAsync_ShouldCallJs_WhenElementHasId()
    {
        var root = new ElementReference("tv-scroll");

        await TvDetailScrollJs.TrySyncAsync(_js, root);

        await _js.Received(1).InvokeAsync<IJSVoidResult>(
            "K7.TvDetailScroll.sync",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], root)));
    }

    [Test]
    public async Task TrySyncAsync_ShouldSwallowJsException()
    {
        var root = new ElementReference("tv-scroll");
        _js.InvokeAsync<IJSVoidResult>("K7.TvDetailScroll.sync", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<IJSVoidResult>(new JSException("root is not an element")));

        var act = async () => await TvDetailScrollJs.TrySyncAsync(_js, root);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task TryDisposeAsync_ShouldCallJs_WhenElementHasId()
    {
        var root = new ElementReference("tv-scroll");

        await TvDetailScrollJs.TryDisposeAsync(_js, root);

        await _js.Received(1).InvokeAsync<IJSVoidResult>(
            "K7.TvDetailScroll.dispose",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], root)));
    }

    [Test]
    public async Task TryDisposeAsync_ShouldSwallowJsException()
    {
        var root = new ElementReference("tv-scroll");
        _js.InvokeAsync<IJSVoidResult>("K7.TvDetailScroll.dispose", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<IJSVoidResult>(new JSDisconnectedException("disconnected")));

        var act = async () => await TvDetailScrollJs.TryDisposeAsync(_js, root);

        await act.Should().NotThrowAsync();
    }
}
