using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class SpatialNavServiceTests
{
    private IJSRuntime _jsRuntime = null!;
    private SpatialNavService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _sut = new SpatialNavService(_jsRuntime);
    }

    [Test]
    public async Task PushLayerAsync_ShouldCallJSInterop()
    {
        // Arrange
        var element = new ElementReference("test-id");

        // Act
        await _sut.PushLayerAsync(element, "popover");

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "SpatialNav.pushLayer",
            Arg.Is<object[]>(args => args.Length == 3
                && args[0] is ElementReference
                && (string)args[1] == "popover"));
    }

    [Test]
    public async Task PopLayerAsync_ShouldCallJSInterop()
    {
        // Arrange
        var element = new ElementReference("test-id");

        // Act
        await _sut.PopLayerAsync(element);

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "SpatialNav.popLayer",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is ElementReference));
    }

    [Test]
    public async Task FocusFirstAsync_ShouldCallJSInterop_WithSelector()
    {
        // Act
        await _sut.FocusFirstAsync(".k7-btn");

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "SpatialNav.focusFirst",
            Arg.Is<object[]>(args => args.Length == 1 && (string)args[0] == ".k7-btn"));
    }

    [Test]
    public async Task FocusFirstAsync_ShouldCallJSInterop_WithNull()
    {
        // Act
        await _sut.FocusFirstAsync();

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "SpatialNav.focusFirst",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] == null));
    }

    [Test]
    public async Task IsFocusInsideAsync_ShouldCallJSInterop()
    {
        // Arrange
        var element = new ElementReference("test-id");
        _jsRuntime.InvokeAsync<bool>("SpatialNav.isFocusInside", Arg.Any<object[]>())
            .Returns(new ValueTask<bool>(true));

        // Act
        var result = await _sut.IsFocusInsideAsync(element);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task PushLayerAsync_WithOptions_ShouldPassOnCloseToJS()
    {
        // Arrange
        var element = new ElementReference("test-id");
        var options = new SpatialNavLayerOptions
        {
            OnClose = new object(),
            FocusSelector = ".first-btn"
        };

        // Act
        await _sut.PushLayerAsync(element, "dialog", options);

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "SpatialNav.pushLayer",
            Arg.Is<object[]>(args => args.Length == 3));
    }
}
