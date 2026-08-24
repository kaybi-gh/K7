using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SubtitleStyleApplicatorTests
{
    private IJSRuntime _js = null!;

    [SetUp]
    public void SetUp()
    {
        _js = Substitute.For<IJSRuntime>();
        _js.InvokeAsync<IJSVoidResult>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSVoidResult>(Substitute.For<IJSVoidResult>()));
    }

    [Test]
    public async Task ApplyAsync_ShouldNotThrow_WhenJsIsDisconnected()
    {
        _js.InvokeAsync<IJSVoidResult>(
                "K7.ensurePlaybackAssets",
                Arg.Any<CancellationToken>(),
                Arg.Any<object?[]>())
            .Returns(_ => ValueTask.FromException<IJSVoidResult>(new JSDisconnectedException("disconnected")));

        var act = async () => await SubtitleStyleApplicator.ApplyAsync(
            _js,
            new VideoPlayerSettingsDto(),
            DeviceType.Desktop);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ApplyAsync_ShouldPassScaledFontSizeToApplySubtitleStyle()
    {
        var settings = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Large };

        await SubtitleStyleApplicator.ApplyAsync(_js, settings, DeviceType.Desktop);

        await _js.Received().InvokeAsync<IJSVoidResult>(
            "applySubtitleStyle",
            Arg.Any<CancellationToken>(),
            Arg.Is<object?[]>(args => args.Length == 1 && PayloadContainsFontSize(args[0], "32px")));
    }

    private static bool PayloadContainsFontSize(object? payload, string expectedFontSize)
    {
        if (payload is null)
            return false;

        var fontSize = payload.GetType().GetProperty("fontSize")?.GetValue(payload) as string;
        return fontSize == expectedFontSize;
    }
}
