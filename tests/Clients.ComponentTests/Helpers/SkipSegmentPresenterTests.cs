using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SkipSegmentPresenterTests
{
    private static readonly DateTime T0 = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    private static readonly MediaSegmentDto Intro = new()
    {
        Type = MediaSegmentType.Intro,
        StartMs = 0,
        EndMs = 90_000
    };

    private static readonly MediaSegmentDto Outro = new()
    {
        Type = MediaSegmentType.Outro,
        StartMs = 1_200_000,
        EndMs = 1_260_000
    };

    private static VideoPlayerSettingsDto Settings(
        IntroSkipBehavior intro = IntroSkipBehavior.ShowButton,
        IntroSkipBehavior outro = IntroSkipBehavior.ShowButton) =>
        new() { IntroSkipBehavior = intro, OutroSkipBehavior = outro };

    [Test]
    public void FindActive_ShouldReturnIntro_WhenTimeInsideIntro()
    {
        SkipSegmentPresenter.FindActive([Intro, Outro], 12).Should().Be(Intro);
    }

    [Test]
    public void FindActive_ShouldReturnNull_WhenTimeOutsideSegments()
    {
        SkipSegmentPresenter.FindActive([Intro, Outro], 200).Should().BeNull();
    }

    [Test]
    public void Tick_ShouldHide_WhenSettingsAreMissing()
    {
        var result = SkipSegmentPresenter.Tick(default, [Intro], null, 5, chromeVisible: false, T0);

        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.None);
        result.State.Visible.Should().BeFalse();
        result.State.ActiveSegment.Should().BeNull();
    }

    [Test]
    public void Tick_ShouldShowButton_WhenIntroShowButtonAndChromeHidden()
    {
        var result = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);

        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.None);
        result.State.Visible.Should().BeTrue();
        result.State.ActiveSegment.Should().Be(Intro);
    }

    [Test]
    public void Tick_ShouldDismissButton_WhenChromeHiddenForDisplayDuration()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);
        var later = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: false, T0 + SkipSegmentPresenter.DisplayDuration);

        later.State.Visible.Should().BeFalse();
        later.State.Dismissed.Should().BeTrue();
    }

    [Test]
    public void Tick_ShouldKeepButton_WhenChromeStaysVisiblePastDisplayDuration()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: true, T0);
        var later = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: true, T0 + SkipSegmentPresenter.DisplayDuration);

        later.State.Visible.Should().BeTrue();
        later.State.Dismissed.Should().BeFalse();
    }

    [Test]
    public void Tick_ShouldReshowButton_WhenChromeBecomesVisibleAfterDismiss()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);
        var dismissed = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: false, T0 + SkipSegmentPresenter.DisplayDuration);
        var chromeShown = SkipSegmentPresenter.Tick(
            dismissed.State, [Intro], Settings(), 9, chromeVisible: true, T0 + TimeSpan.FromSeconds(6));

        chromeShown.State.Visible.Should().BeTrue();
        chromeShown.State.ActiveSegment.Should().Be(Intro);
    }

    [Test]
    public void Tick_ShouldHide_WhenBehaviorDisabled()
    {
        var result = SkipSegmentPresenter.Tick(
            default, [Intro], Settings(IntroSkipBehavior.Disabled), 5, chromeVisible: true, T0);

        result.State.Visible.Should().BeFalse();
        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.None);
    }

    [Test]
    public void Tick_ShouldAutoSkip_WhenIntroBehaviorIsAutoSkip()
    {
        var result = SkipSegmentPresenter.Tick(
            default, [Intro], Settings(IntroSkipBehavior.AutoSkip), 5, chromeVisible: false, T0);

        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.AutoSkip);
        result.State.Visible.Should().BeFalse();
        result.State.AutoSkipped.Should().BeTrue();
        result.State.ActiveSegment.Should().Be(Intro);
    }

    [Test]
    public void Tick_ShouldNotAutoSkipAgain_WhenCooldownActive()
    {
        var first = SkipSegmentPresenter.Tick(
            default, [Intro], Settings(IntroSkipBehavior.AutoSkip), 5, chromeVisible: false, T0);
        var stillInIntro = first.State with { AutoSkipped = false };
        var second = SkipSegmentPresenter.Tick(
            stillInIntro, [Intro], Settings(IntroSkipBehavior.AutoSkip), 6, chromeVisible: false, T0.AddSeconds(1));

        second.Action.Should().Be(SkipSegmentPresenter.ActionKind.None);
        second.State.Visible.Should().BeFalse();
    }

    [Test]
    public void Tick_ShouldAutoSkipOutro_WhenOutroBehaviorIsAutoSkip()
    {
        var settings = Settings(intro: IntroSkipBehavior.ShowButton, outro: IntroSkipBehavior.AutoSkip);
        var result = SkipSegmentPresenter.Tick(default, [Intro, Outro], settings, 1205, chromeVisible: false, T0);

        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.AutoSkip);
        result.State.ActiveSegment.Should().Be(Outro);
    }

    [Test]
    public void Tick_ShouldKeepFloatingButtonHidden_WhenStillInIntroAfterDismissAndChromeHidden()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);
        var dismissed = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: false, T0 + SkipSegmentPresenter.DisplayDuration);
        var later = SkipSegmentPresenter.Tick(
            dismissed.State, [Intro], Settings(), 40, chromeVisible: false, T0 + TimeSpan.FromSeconds(40));

        later.State.Visible.Should().BeFalse();
        later.State.Dismissed.Should().BeTrue();
        later.State.ActiveSegment.Should().Be(Intro);
    }

    [Test]
    public void Tick_ShouldReshowInOverlay_WhenChromeVisibleLateInIntro()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);
        var dismissed = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: false, T0 + SkipSegmentPresenter.DisplayDuration);
        var overlay = SkipSegmentPresenter.Tick(
            dismissed.State, [Intro], Settings(), 80, chromeVisible: true, T0 + TimeSpan.FromSeconds(80));

        overlay.State.Visible.Should().BeTrue();
        overlay.State.ActiveSegment.Should().Be(Intro);
    }

    [Test]
    public void Tick_ShouldNotShowButton_WhenAutoSkip()
    {
        var result = SkipSegmentPresenter.Tick(
            default, [Intro], Settings(IntroSkipBehavior.AutoSkip), 5, chromeVisible: true, T0);

        result.State.Visible.Should().BeFalse();
        result.Action.Should().Be(SkipSegmentPresenter.ActionKind.AutoSkip);
    }

    [Test]
    public void Tick_ShouldResetDismissed_WhenPlaybackLeavesThenReentersSegment()
    {
        var shown = SkipSegmentPresenter.Tick(default, [Intro], Settings(), 5, chromeVisible: false, T0);
        var dismissed = SkipSegmentPresenter.Tick(
            shown.State, [Intro], Settings(), 8, chromeVisible: false, T0 + SkipSegmentPresenter.DisplayDuration);
        var left = SkipSegmentPresenter.Tick(
            dismissed.State, [Intro], Settings(), 200, chromeVisible: false, T0 + TimeSpan.FromSeconds(10));
        var reentered = SkipSegmentPresenter.Tick(
            left.State, [Intro], Settings(), 4, chromeVisible: false, T0 + TimeSpan.FromSeconds(11));

        left.State.ActiveSegment.Should().BeNull();
        reentered.State.Visible.Should().BeTrue();
        reentered.State.Dismissed.Should().BeFalse();
    }

    [Test]
    public void Tick_ShouldShowIntro_WhenStateResetAfterPreviousEpisodeOutro()
    {
        var outroShown = SkipSegmentPresenter.Tick(
            default, [Intro, Outro], Settings(), 1205, chromeVisible: false, T0);
        var outroDismissed = SkipSegmentPresenter.Tick(
            outroShown.State, [Intro, Outro], Settings(), 1208, chromeVisible: false,
            T0 + SkipSegmentPresenter.DisplayDuration);

        outroDismissed.State.Dismissed.Should().BeTrue();

        var nextEpisode = SkipSegmentPresenter.Tick(
            default, [Intro, Outro], Settings(), 5, chromeVisible: false, T0.AddMinutes(1));

        nextEpisode.State.Visible.Should().BeTrue();
        nextEpisode.State.Dismissed.Should().BeFalse();
        nextEpisode.State.ActiveSegment.Should().Be(Intro);
    }
}
