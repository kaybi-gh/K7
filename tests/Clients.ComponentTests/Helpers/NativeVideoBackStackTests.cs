using FluentAssertions;
using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeVideoBackStackTests
{
    [Test]
    public void Evaluate_ShouldCancelSeek_WhenScrubbing()
    {
        var (action, cancelSeek, _, _) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            SeekScrubbing = true,
            UtcNow = DateTime.UtcNow
        });

        action.Should().Be(NativeVideoBackAction.Consumed);
        cancelSeek.Should().BeTrue();
    }

    [Test]
    public void Evaluate_ShouldCloseVolume_WhenVolumeOpen()
    {
        var (_, _, _, closeVolume) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            VolumeOpen = true,
            UtcNow = DateTime.UtcNow
        });

        closeVolume.Should().BeTrue();
    }

    [Test]
    public void Evaluate_ShouldHidePlayer_WhenIdleAfterChromeDismissed()
    {
        var (action, _, _, _) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            ShowChrome = false,
            PlaybackState = PlaybackState.Idle,
            UtcNow = DateTime.UtcNow
        });

        action.Should().Be(NativeVideoBackAction.HidePlayerAsync);
    }

    [Test]
    public void GetSpriteCell_ShouldMatchSeekBarGrid()
    {
        var (col, row) = NativeSeekThumbnailHelper.GetSpriteCell(95);
        col.Should().Be(3);
        row.Should().Be(0);
    }

    [Test]
    public void Evaluate_ShouldBeConsumedBySettings_BeforeAnythingElse()
    {
        var (action, cancelSeek, hideChrome, closeVolume) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            SettingsHandledBack = true,
            VolumeOpen = true,
            SeekScrubbing = true,
            ShowChrome = true,
            UtcNow = DateTime.UtcNow
        });

        action.Should().Be(NativeVideoBackAction.Consumed);
        cancelSeek.Should().BeFalse();
        hideChrome.Should().BeFalse();
        closeVolume.Should().BeFalse();
    }

    [Test]
    public void Evaluate_ShouldHideChrome_WhenChromeVisibleAndNothingElseActive()
    {
        var (action, cancelSeek, hideChrome, closeVolume) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            ShowChrome = true,
            UtcNow = DateTime.UtcNow
        });

        action.Should().Be(NativeVideoBackAction.Consumed);
        hideChrome.Should().BeTrue();
        cancelSeek.Should().BeFalse();
        closeVolume.Should().BeFalse();
    }

    [Test]
    public void Evaluate_ShouldClosePlayer_WhenPlayingAndChromeAlreadyHidden()
    {
        var (action, _, _, _) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            ShowChrome = false,
            PlaybackState = PlaybackState.Playing,
            UtcNow = DateTime.UtcNow
        });

        action.Should().Be(NativeVideoBackAction.ClosePlayer);
    }

    [Test]
    public void Evaluate_ShouldSuppressClose_WithinSuppressWindow()
    {
        var now = DateTime.UtcNow;
        var (action, _, _, _) = NativeVideoBackStack.Evaluate(new NativeVideoBackContext
        {
            ShowChrome = false,
            PlaybackState = PlaybackState.Playing,
            UtcNow = now,
            SuppressCloseUntil = now.AddMilliseconds(200)
        });

        action.Should().Be(NativeVideoBackAction.Consumed);
    }
}
