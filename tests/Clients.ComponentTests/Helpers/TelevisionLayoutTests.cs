using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TelevisionLayoutTests
{
    [Test]
    public void MatchesAndroidTelevision_ShouldBeTrue_WhenOnlyFireTvFeature()
    {
        TelevisionLayout.MatchesAndroidTelevision(
            uiModeTelevision: false,
            hasLeanback: false,
            hasFireTvFeature: true,
            model: "Pixel").Should().BeTrue();
    }

    [Test]
    public void MatchesAndroidTelevision_ShouldBeTrue_WhenFireStickModelWithoutUiMode()
    {
        TelevisionLayout.MatchesAndroidTelevision(
            uiModeTelevision: false,
            hasLeanback: false,
            hasFireTvFeature: false,
            model: "AFTKA").Should().BeTrue();
    }

    [Test]
    public void MatchesAndroidTelevision_ShouldBeFalse_WhenPhone()
    {
        TelevisionLayout.MatchesAndroidTelevision(
            uiModeTelevision: false,
            hasLeanback: false,
            hasFireTvFeature: false,
            model: "Pixel 8").Should().BeFalse();
    }

    [Test]
    public void UserAgentLooksLikeTelevision_ShouldBeTrue_WhenK7TvMarker()
    {
        TelevisionLayout.UserAgentLooksLikeTelevision(
            "Mozilla/5.0 (Linux; Android 12) Chrome/120.0.0.0 K7TV/1.0")
            .Should().BeTrue();
    }

    [Test]
    public void UserAgentLooksLikeTelevision_ShouldBeTrue_WhenFireStickModel()
    {
        TelevisionLayout.UserAgentLooksLikeTelevision(
            "Mozilla/5.0 (Linux; Android 9; AFTKA Build/PS7234) AppleWebKit/537.36")
            .Should().BeTrue();
    }

    [Test]
    public void UserAgentLooksLikeTelevision_ShouldBeTrue_WhenAndroidTvToken()
    {
        TelevisionLayout.UserAgentLooksLikeTelevision(
            "Mozilla/5.0 (Linux; Android 12; Android TV) AppleWebKit/537.36")
            .Should().BeTrue();
    }

    [Test]
    public void UserAgentLooksLikeTelevision_ShouldBeFalse_WhenPhoneChrome()
    {
        TelevisionLayout.UserAgentLooksLikeTelevision(
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36")
            .Should().BeFalse();
    }

    [Test]
    public void TryGetViewportScale_ShouldSkip_WhenAlready1920()
    {
        TelevisionLayout.TryGetViewportScale(1920, out var scale).Should().BeFalse();
        scale.Should().Be(1);
    }

    [Test]
    public void TryGetViewportScale_ShouldZoomOut_When1080pReportsPhoneCssWidth()
    {
        TelevisionLayout.TryGetViewportScale(960, out var scale).Should().BeTrue();
        scale.Should().BeApproximately(0.5, 0.001);
    }

    [Test]
    public void TryGetViewportScale_ShouldZoomIn_When4kReportsPhysicalCssWidth()
    {
        TelevisionLayout.TryGetViewportScale(3840, out var scale).Should().BeTrue();
        scale.Should().BeApproximately(2, 0.001);
    }
}
